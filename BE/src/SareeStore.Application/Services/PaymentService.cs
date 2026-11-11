using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Payment;
using SareeStore.Application.DTOs.Payment;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;

namespace SareeStore.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IValidator<InitiatePaymentRequest> _initiateValidator;
    private readonly IValidator<VerifyPaymentRequest> _verifyValidator;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IPaymentProvider paymentProvider,
        IValidator<InitiatePaymentRequest> initiateValidator,
        IValidator<VerifyPaymentRequest> verifyValidator)
    {
        _paymentRepository = paymentRepository;
        _paymentProvider = paymentProvider;
        _initiateValidator = initiateValidator;
        _verifyValidator = verifyValidator;
    }

    public async Task<PaymentInitiateResponse> InitiatePaymentAsync(
        int customerId,
        int orderId,
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate request
        var validationResult = await _initiateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException(errors);
        }

        // 2. Load order and validate ownership
        var order = await _paymentRepository.GetOrderForPaymentAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        if (order.CustomerId != customerId)
        {
            throw new UnauthorizedException("You do not have permission to initiate payment for this order.");
        }

        // 3. Validate order payment method & status
        if (order.Payments.Any(p => p.PaymentMethod == PaymentMethod.COD) || request.PaymentMethod == PaymentMethod.COD)
        {
            throw new ValidationException("Cash on Delivery orders cannot be processed through online payment initiation.");
        }

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            throw new ValidationException("Cannot initiate payment for a cancelled order.");
        }

        if (order.OrderStatus != OrderStatus.Placed)
        {
            throw new ValidationException($"Cannot initiate payment for order in '{order.OrderStatus}' status.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            throw new ConflictException("This order has already been paid.");
        }

        // 4. Validate 30-minute payment window
        var elapsed = DateTime.UtcNow - order.OrderDateUtc;
        if (elapsed > TimeSpan.FromMinutes(30))
        {
            throw new ValidationException("The payment window for this order has expired. Please place a new order.");
        }

        // 5. Idempotency Key check and session recovery
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingIdempotentPayment = await _paymentRepository.GetByIdempotencyKeyAsync(orderId, request.IdempotencyKey, cancellationToken);
            if (existingIdempotentPayment != null)
            {
                if (existingIdempotentPayment.PaymentMethod != request.PaymentMethod)
                {
                    throw new ConflictException("The provided idempotency key was already used with a different payment method for this order.");
                }

                if (existingIdempotentPayment.PaymentStatus == PaymentStatus.Processing)
                {
                    // If TransactionId is missing (from previous crash/timeout before saving response), recover it
                    if (string.IsNullOrWhiteSpace(existingIdempotentPayment.TransactionId))
                    {
                        var recoveryResult = await _paymentProvider.InitiatePaymentAsync(new PaymentInitiateRequest
                        {
                            OrderId = order.Id,
                            PaymentId = existingIdempotentPayment.Id,
                            Amount = existingIdempotentPayment.Amount,
                            Currency = "INR",
                            PaymentMethod = existingIdempotentPayment.PaymentMethod,
                            IdempotencyKey = existingIdempotentPayment.IdempotencyKey
                        }, cancellationToken);

                        if (recoveryResult.IsSuccess)
                        {
                            existingIdempotentPayment.TransactionId = recoveryResult.ProviderPaymentReference;
                            existingIdempotentPayment.FailureReason = null;
                            await _paymentRepository.UpdateAsync(existingIdempotentPayment, cancellationToken);
                        }
                    }

                    return new PaymentInitiateResponse
                    {
                        PaymentId = existingIdempotentPayment.Id,
                        OrderId = existingIdempotentPayment.OrderId,
                        ProviderName = existingIdempotentPayment.PaymentProvider,
                        PaymentReference = existingIdempotentPayment.TransactionId ?? string.Empty,
                        Amount = existingIdempotentPayment.Amount,
                        Currency = "INR",
                        Status = existingIdempotentPayment.PaymentStatus.ToString(),
                        ExpiresAtUtc = order.OrderDateUtc.AddMinutes(30)
                    };
                }

                if (existingIdempotentPayment.PaymentStatus == PaymentStatus.Paid)
                {
                    throw new ConflictException("This payment has already been successfully processed.");
                }

                if (existingIdempotentPayment.PaymentStatus == PaymentStatus.Failed)
                {
                    throw new ConflictException("The provided idempotency key was already used for a failed attempt. Please provide a new idempotency key to retry.");
                }
            }
        }

        // 6. Check for active Processing attempt
        var activeAttempt = await _paymentRepository.GetActiveProcessingPaymentAsync(orderId, cancellationToken);
        if (activeAttempt != null)
        {
            throw new ConflictException("A payment attempt is currently in progress for this order. Please complete or verify that attempt.");
        }

        // 7. Persist payment attempt record
        var payment = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Processing,
            Amount = order.TotalAmount,
            PaymentProvider = _paymentProvider.ProviderName,
            IdempotencyKey = request.IdempotencyKey
        };

        // Persist payment attempt in database (enforcing filtered unique index)
        await _paymentRepository.AddAsync(payment, cancellationToken);

        // 8. Call payment provider (outside of DB transaction)
        PaymentInitiateResult providerResult;
        try
        {
            providerResult = await _paymentProvider.InitiatePaymentAsync(new PaymentInitiateRequest
            {
                OrderId = order.Id,
                PaymentId = payment.Id,
                Amount = payment.Amount,
                Currency = "INR",
                PaymentMethod = payment.PaymentMethod,
                IdempotencyKey = payment.IdempotencyKey
            }, cancellationToken);
        }
        catch (TimeoutException)
        {
            // Ambiguous timeout: do not mark Failed as provider may have accepted session
            payment.FailureReason = "PROVIDER_INITIATION_TIMEOUT: Network timeout contacting payment provider. Reconcile with idempotency key.";
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            throw new ConflictException("Payment provider initiation timed out. The attempt remains in processing and can be retried with the same idempotency key.");
        }
        catch (Exception ex)
        {
            // Ambiguous error: do not mark Failed as provider may have accepted session
            payment.FailureReason = "PROVIDER_INITIATION_AMBIGUOUS: " + ex.Message;
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            throw new ConflictException("Payment provider initiation encountered an unconfirmed error. Please retry with the same idempotency key.");
        }

        if (!providerResult.IsSuccess)
        {
            // Definitive gateway rejection
            payment.PaymentStatus = PaymentStatus.Failed;
            payment.FailureReason = providerResult.ErrorMessage ?? "Payment provider declined initiation.";
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            throw new ConflictException($"Payment initiation was declined: {payment.FailureReason}");
        }

        // 9. Update payment with provider reference
        payment.TransactionId = providerResult.ProviderPaymentReference;
        payment.FailureReason = null;
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        return new PaymentInitiateResponse
        {
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            ProviderName = payment.PaymentProvider,
            PaymentReference = payment.TransactionId,
            Amount = payment.Amount,
            Currency = "INR",
            Status = payment.PaymentStatus.ToString(),
            ExpiresAtUtc = providerResult.ExpiresAtUtc ?? order.OrderDateUtc.AddMinutes(30)
        };
    }

    public async Task<PaymentVerifyResponse> VerifyPaymentAsync(
        int customerId,
        int paymentId,
        VerifyPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate request
        var validationResult = await _verifyValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new ValidationException(errors);
        }

        // 2. Load payment with order and validate ownership
        var payment = await _paymentRepository.GetByIdWithOrderAsync(paymentId, cancellationToken);
        if (payment == null)
        {
            throw new NotFoundException($"Payment with ID {paymentId} was not found.");
        }

        var order = payment.Order;
        if (order.CustomerId != customerId)
        {
            throw new UnauthorizedException("You do not have permission to verify this payment.");
        }

        // 3. Idempotent check: if already Paid, return existing record
        if (payment.PaymentStatus == PaymentStatus.Paid)
        {
            return new PaymentVerifyResponse
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Status = payment.PaymentStatus.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                TransactionId = payment.TransactionId,
                FailureReason = payment.FailureReason,
                PaidAtUtc = payment.PaidAtUtc,
                IsLateSuccess = payment.IsLateSuccess,
                RequiresRefund = payment.RequiresRefund,
                RefundReason = payment.RefundReason
            };
        }

        if (payment.PaymentStatus == PaymentStatus.Failed)
        {
            throw new ConflictException("This payment attempt has already failed and cannot be verified.");
        }

        if (payment.PaymentStatus != PaymentStatus.Processing)
        {
            throw new ValidationException($"Payment attempt in '{payment.PaymentStatus}' status is not eligible for verification.");
        }

        // 4. Verify payment reference matches payment attempt
        if (!string.Equals(payment.TransactionId, request.PaymentReference, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("The payment reference does not match this payment attempt.");
        }

        // 5. Call payment provider to verify
        var verifyResult = await _paymentProvider.VerifyPaymentAsync(new PaymentVerifyRequest
        {
            ProviderPaymentReference = request.PaymentReference,
            ExpectedAmount = payment.Amount,
            ExpectedCurrency = "INR"
        }, cancellationToken);

        var isWindowExpired = (DateTime.UtcNow - order.OrderDateUtc) > TimeSpan.FromMinutes(30);

        // 6. Handle provider outcome
        if (verifyResult.IsSuccess)
        {
            // Check for late payment success on already cancelled order
            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                payment.PaymentStatus = PaymentStatus.Paid;
                payment.PaidAtUtc = DateTime.UtcNow;
                payment.TransactionId = verifyResult.TransactionId ?? payment.TransactionId;
                payment.IsLateSuccess = true;
                payment.RequiresRefund = true;
                payment.RefundReason = "LATE_SUCCESS_AFTER_CANCELLATION: Order was already cancelled. Identified for refund.";
                payment.FailureReason = null;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);

                return new PaymentVerifyResponse
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    Status = payment.PaymentStatus.ToString(),
                    OrderStatus = order.OrderStatus.ToString(),
                    TransactionId = payment.TransactionId,
                    FailureReason = null,
                    PaidAtUtc = payment.PaidAtUtc,
                    IsLateSuccess = true,
                    RequiresRefund = true,
                    RefundReason = payment.RefundReason
                };
            }

            // Check for late payment success after 30-minute expiry window (order not cancelled yet)
            if (isWindowExpired)
            {
                payment.PaymentStatus = PaymentStatus.Paid;
                payment.PaidAtUtc = DateTime.UtcNow;
                payment.TransactionId = verifyResult.TransactionId ?? payment.TransactionId;
                payment.IsLateSuccess = true;
                payment.RequiresRefund = true;
                payment.RefundReason = "LATE_SUCCESS_AFTER_EXPIRY: Payment completed after 30-minute payment window expired. Identified for refund.";
                payment.FailureReason = null;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);

                return new PaymentVerifyResponse
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    Status = payment.PaymentStatus.ToString(),
                    OrderStatus = order.OrderStatus.ToString(), // Remains Placed, not confirmed!
                    TransactionId = payment.TransactionId,
                    FailureReason = null,
                    PaidAtUtc = payment.PaidAtUtc,
                    IsLateSuccess = true,
                    RequiresRefund = true,
                    RefundReason = payment.RefundReason
                };
            }

            // Normal on-time success: transition order and payment atomically
            var finalTxnId = verifyResult.TransactionId ?? payment.TransactionId ?? request.PaymentReference;
            var (updatedPayment, updatedOrder) = await _paymentRepository.ProcessSuccessfulPaymentAsync(
                payment.Id,
                finalTxnId,
                cancellationToken);

            return new PaymentVerifyResponse
            {
                PaymentId = updatedPayment.Id,
                OrderId = updatedPayment.OrderId,
                Status = updatedPayment.PaymentStatus.ToString(),
                OrderStatus = updatedOrder.OrderStatus.ToString(),
                TransactionId = updatedPayment.TransactionId,
                FailureReason = null,
                PaidAtUtc = updatedPayment.PaidAtUtc,
                IsLateSuccess = false,
                RequiresRefund = false,
                RefundReason = null
            };
        }
        else
        {
            // Definitive failure: mark payment as Failed, keep Order as Placed
            payment.PaymentStatus = PaymentStatus.Failed;
            payment.FailureReason = verifyResult.FailureReason ?? "Payment verification failed.";
            payment.IsLateSuccess = false;
            payment.RequiresRefund = false;
            payment.RefundReason = null;
            await _paymentRepository.UpdateAsync(payment, cancellationToken);

            return new PaymentVerifyResponse
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                Status = payment.PaymentStatus.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                TransactionId = payment.TransactionId,
                FailureReason = payment.FailureReason,
                PaidAtUtc = null,
                IsLateSuccess = false,
                RequiresRefund = false,
                RefundReason = null
            };
        }
    }

    public async Task<IReadOnlyList<PaymentHistoryResponse>> GetOrderPaymentsAsync(
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
            throw new UnauthorizedException("You do not have permission to view payments for this order.");
        }

        var payments = await _paymentRepository.GetByOrderIdAsync(orderId, cancellationToken);

        return payments.Select(p => new PaymentHistoryResponse
        {
            Id = p.Id,
            OrderId = p.OrderId,
            PaymentMethod = p.PaymentMethod.ToString(),
            PaymentProvider = p.PaymentProvider,
            TransactionId = p.TransactionId,
            Amount = p.Amount,
            Currency = "INR",
            PaymentStatus = p.PaymentStatus.ToString(),
            FailureReason = p.FailureReason,
            CreatedAtUtc = p.CreatedAtUtc,
            PaidAtUtc = p.PaidAtUtc,
            IsLateSuccess = p.IsLateSuccess,
            RequiresRefund = p.RequiresRefund,
            RefundReason = p.RefundReason
        }).ToList();
    }
}
