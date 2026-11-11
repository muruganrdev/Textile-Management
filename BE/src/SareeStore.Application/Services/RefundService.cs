using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Refund;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;

namespace SareeStore.Application.Services;

public class RefundService : IRefundService
{
    private readonly IRefundRepository _refundRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IRefundProvider _refundProvider;

    public RefundService(
        IRefundRepository refundRepository,
        IPaymentRepository paymentRepository,
        IRefundProvider refundProvider)
    {
        _refundRepository = refundRepository;
        _paymentRepository = paymentRepository;
        _refundProvider = refundProvider;
    }

    public async Task<RefundResponse> ProcessPendingRefundAsync(
        int refundId,
        CancellationToken cancellationToken = default)
    {
        var refund = await _refundRepository.GetByIdAsync(refundId, cancellationToken);
        if (refund == null)
        {
            throw new NotFoundException($"Refund with ID {refundId} was not found.");
        }

        // 1. Idempotency fast path
        if (refund.Status == RefundStatus.Success)
        {
            return MapToResponse(refund);
        }

        // 2. Transition from Pending to Processing
        if (refund.Status == RefundStatus.Pending)
        {
            refund.Status = RefundStatus.Processing;
            await _refundRepository.UpdateAsync(refund, cancellationToken);
        }

        var payment = refund.Payment ?? await _paymentRepository.GetByIdAsync(refund.PaymentId, cancellationToken);
        if (payment == null)
        {
            throw new NotFoundException($"Payment with ID {refund.PaymentId} was not found.");
        }

        // 3. Initiate with Refund Provider outside of DB transaction using stable IdempotencyKey
        RefundInitiateResult providerResult;
        try
        {
            providerResult = await _refundProvider.InitiateRefundAsync(new RefundInitiateRequest
            {
                OrderId = refund.OrderId,
                PaymentId = refund.PaymentId,
                Amount = refund.Amount,
                Currency = refund.Currency,
                PaymentTransactionId = payment.TransactionId ?? string.Empty,
                IdempotencyKey = refund.IdempotencyKey ?? $"REF-ORD{refund.OrderId}-PAY{refund.PaymentId}",
                Reason = refund.Reason
            }, cancellationToken);
        }
        catch (TimeoutException)
        {
            refund.Status = RefundStatus.Processing;
            refund.FailureReason = "REFUND_TIMEOUT: Refund gateway initiation timed out. Marked Processing for reconciliation.";
            await _refundRepository.UpdateAsync(refund, cancellationToken);
            return MapToResponse(refund);
        }
        catch (Exception ex)
        {
            refund.Status = RefundStatus.Processing;
            refund.FailureReason = $"REFUND_UNCONFIRMED: {ex.Message}. Marked Processing for reconciliation.";
            await _refundRepository.UpdateAsync(refund, cancellationToken);
            return MapToResponse(refund);
        }

        // 4. Handle provider outcome
        if (providerResult.IsSuccess)
        {
            var processed = await _refundRepository.ProcessSuccessfulRefundAsync(
                refund.Id,
                providerResult.RefundReference ?? $"SIM-REF-ORD{refund.OrderId}-PAY{refund.PaymentId}",
                cancellationToken);
            return MapToResponse(processed);
        }
        else
        {
            refund.Status = RefundStatus.Failed;
            refund.FailureReason = providerResult.ErrorMessage ?? "Refund declined by provider.";
            refund.RefundReference = providerResult.RefundReference;
            await _refundRepository.UpdateAsync(refund, cancellationToken);
            return MapToResponse(refund);
        }
    }

    public async Task<RefundResponse> InitiateRefundForOrderCancellationAsync(
        int orderId,
        int paymentId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"REF-ORD{orderId}-PAY{paymentId}-CANCEL";

        // 1. Idempotency check: return or continue existing refund if already recorded
        var existing = await _refundRepository.GetByIdempotencyKeyAsync(paymentId, idempotencyKey, cancellationToken);
        if (existing != null)
        {
            if (existing.Status == RefundStatus.Success)
            {
                return MapToResponse(existing);
            }

            return await ProcessPendingRefundAsync(existing.Id, cancellationToken);
        }

        var payment = await _paymentRepository.GetByIdAsync(paymentId, cancellationToken);
        if (payment == null)
        {
            throw new NotFoundException($"Payment with ID {paymentId} was not found.");
        }

        // 2. Create initial Refund record in Pending status
        var refund = new Refund
        {
            OrderId = orderId,
            PaymentId = paymentId,
            Amount = amount,
            Currency = "INR",
            Status = RefundStatus.Pending,
            Reason = reason,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _refundRepository.AddAsync(refund, cancellationToken);

        // 3. Process the refund operation outside of DB transaction
        return await ProcessPendingRefundAsync(refund.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<RefundResponse>> GetOrderRefundsAsync(
        int customerId,
        int orderId,
        bool isEmployeeOrAdmin,
        CancellationToken cancellationToken = default)
    {
        var order = await _paymentRepository.GetOrderForPaymentAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        if (!isEmployeeOrAdmin && order.CustomerId != customerId)
        {
            throw new ForbiddenException("You do not have permission to view refunds for this order.");
        }

        var refunds = await _refundRepository.GetByOrderIdAsync(orderId, cancellationToken);
        return refunds.Select(MapToResponse).ToList();
    }

    public async Task<RefundResponse> RetryOrReconcileRefundAsync(
        int refundId,
        CancellationToken cancellationToken = default)
    {
        return await RetryOrReconcileRefundAsync(null, refundId, cancellationToken);
    }

    public async Task<RefundResponse> RetryOrReconcileRefundAsync(
        int? customerId,
        int refundId,
        CancellationToken cancellationToken = default)
    {
        var refund = await _refundRepository.GetByIdAsync(refundId, cancellationToken);
        if (refund == null)
        {
            throw new NotFoundException($"Refund with ID {refundId} was not found.");
        }

        // Customer ownership verification
        if (customerId.HasValue && refund.Order != null && refund.Order.CustomerId != customerId.Value)
        {
            throw new ForbiddenException("You do not have permission to access or reconcile refunds for this order.");
        }

        // 1. Success is already terminal and idempotent
        if (refund.Status == RefundStatus.Success)
        {
            return MapToResponse(refund);
        }

        // 2. Pending refunds: initiate through provider
        if (refund.Status == RefundStatus.Pending)
        {
            return await ProcessPendingRefundAsync(refund.Id, cancellationToken);
        }

        // 3. Processing refunds: reconcile with provider or re-initiate
        if (refund.Status == RefundStatus.Processing)
        {
            if (!string.IsNullOrWhiteSpace(refund.RefundReference))
            {
                try
                {
                    var verifyResult = await _refundProvider.VerifyRefundAsync(new RefundVerifyRequest
                    {
                        RefundReference = refund.RefundReference,
                        ExpectedAmount = refund.Amount,
                        ExpectedCurrency = refund.Currency
                    }, cancellationToken);

                    if (verifyResult.IsSuccess)
                    {
                        var processed = await _refundRepository.ProcessSuccessfulRefundAsync(
                            refund.Id, refund.RefundReference, cancellationToken);
                        return MapToResponse(processed);
                    }
                    else
                    {
                        refund.Status = RefundStatus.Failed;
                        refund.FailureReason = verifyResult.FailureReason ?? "Refund verification failed.";
                        await _refundRepository.UpdateAsync(refund, cancellationToken);
                        return MapToResponse(refund);
                    }
                }
                catch (TimeoutException)
                {
                    refund.Status = RefundStatus.Processing;
                    refund.FailureReason = "REFUND_TIMEOUT: Refund gateway verification timed out. Remains Processing for reconciliation.";
                    await _refundRepository.UpdateAsync(refund, cancellationToken);
                    return MapToResponse(refund);
                }
                catch (Exception ex)
                {
                    refund.Status = RefundStatus.Processing;
                    refund.FailureReason = $"REFUND_UNCONFIRMED: {ex.Message}. Remains Processing for reconciliation.";
                    await _refundRepository.UpdateAsync(refund, cancellationToken);
                    return MapToResponse(refund);
                }
            }

            // Reference was missing (e.g. initial initiation timed out before response was received): re-initiate using stable IdempotencyKey
            return await ProcessPendingRefundAsync(refund.Id, cancellationToken);
        }

        // 4. Failed refunds: explicit retry policy
        if (refund.Status == RefundStatus.Failed)
        {
            if (refund.Payment != null && refund.Payment.PaymentStatus == PaymentStatus.Refunded)
            {
                throw new ConflictException("Payment has already been fully refunded. Cannot retry failed refund.");
            }

            // Reset status back to Processing and re-invoke provider with original stable idempotency key
            refund.Status = RefundStatus.Processing;
            refund.FailureReason = null;
            await _refundRepository.UpdateAsync(refund, cancellationToken);

            return await ProcessPendingRefundAsync(refund.Id, cancellationToken);
        }

        return MapToResponse(refund);
    }

    private static RefundResponse MapToResponse(Refund refund)
    {
        return new RefundResponse
        {
            Id = refund.Id,
            OrderId = refund.OrderId,
            PaymentId = refund.PaymentId,
            RefundReference = refund.RefundReference,
            Amount = refund.Amount,
            Currency = refund.Currency,
            Status = refund.Status.ToString(),
            Reason = refund.Reason,
            FailureReason = refund.FailureReason,
            CreatedAtUtc = refund.CreatedAtUtc,
            RefundedAtUtc = refund.RefundedAtUtc
        };
    }
}
