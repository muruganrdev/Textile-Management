using System.Data;
using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _context;

    public PaymentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public async Task<Payment?> GetByIdWithOrderAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> GetActiveProcessingPaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.PaymentStatus == PaymentStatus.Processing, cancellationToken);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(int orderId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        try
        {
            await _context.Payments.AddAsync(payment, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return payment;
        }
        catch (DbUpdateException ex)
        {
            _context.ChangeTracker.Clear();
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            if (innerMsg.Contains("IX_Payments_OrderId_Processing", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("A payment attempt is already in progress for this order.");
            }
            if (innerMsg.Contains("IX_Payments_OrderId_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("The idempotency key has already been used for this order.");
            }
            if (innerMsg.Contains("IX_Payments_OrderId_Paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("A successful payment has already been recorded for this order.");
            }
            throw new ConflictException("Payment attempt could not be created due to a concurrent conflict.");
        }
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetOrderForPaymentAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public async Task<(Payment payment, Order order)> ProcessSuccessfulPaymentAsync(
        int paymentId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var payment = await _context.Payments
                .FromSql($"SELECT * FROM Payments WITH (UPDLOCK, ROWLOCK) WHERE Id = {paymentId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
            {
                throw new NotFoundException($"Payment with ID {paymentId} was not found.");
            }

            var order = await _context.Orders
                .FromSql($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {payment.OrderId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID {payment.OrderId} was not found.");
            }

            payment.Order = order;

            // Idempotent check: If payment is already Paid, commit and return without duplicating history
            if (payment.PaymentStatus == PaymentStatus.Paid)
            {
                await transaction.CommitAsync(cancellationToken);
                return (payment, order);
            }

            // Check if order was already paid by another payment attempt
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConflictException("A successful payment has already been recorded for this order.");
            }

            // Check if order was already cancelled before payment lock was acquired
            if (order.OrderStatus == OrderStatus.Cancelled)
            {
                payment.PaymentStatus = PaymentStatus.Paid;
                payment.PaidAtUtc = DateTime.UtcNow;
                payment.TransactionId = transactionId;
                payment.FailureReason = null;
                payment.IsLateSuccess = true;
                payment.RequiresRefund = true;
                payment.RefundReason = "LATE_SUCCESS_AFTER_CANCELLATION: Order was already cancelled. Identified for refund.";

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (payment, order);
            }

            // 1. Update Payment entity
            payment.PaymentStatus = PaymentStatus.Paid;
            payment.PaidAtUtc = DateTime.UtcNow;
            payment.TransactionId = transactionId;
            payment.FailureReason = null;
            payment.IsLateSuccess = false;
            payment.RequiresRefund = false;
            payment.RefundReason = null;

            // 2. Update Order entity
            order.PaymentStatus = PaymentStatus.Paid;

            // 3. If Order is Placed, transition to Confirmed with OrderStatusHistory
            if (order.OrderStatus == OrderStatus.Placed)
            {
                order.OrderStatus = OrderStatus.Confirmed;

                var history = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    FromStatus = OrderStatus.Placed,
                    ToStatus = OrderStatus.Confirmed,
                    Remarks = $"Payment verified successfully via {payment.PaymentProvider} (Txn: {transactionId}).",
                    ChangedAtUtc = DateTime.UtcNow
                };

                await _context.OrderStatusHistories.AddAsync(history, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (payment, order);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            if (ex.InnerException?.Message.Contains("IX_Payments_OrderId_Paid", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new ConflictException("A successful payment has already been recorded for this order.");
            }

            throw new ConflictException("A concurrency conflict occurred while processing payment verification. Please retry.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
