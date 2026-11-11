using System.Data;
using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

public class RefundRepository : IRefundRepository
{
    private readonly ApplicationDbContext _context;

    public RefundRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Refund> AddAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        try
        {
            await _context.Refunds.AddAsync(refund, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return refund;
        }
        catch (DbUpdateException ex)
        {
            _context.ChangeTracker.Clear();
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            if (innerMsg.Contains("IX_Refunds_PaymentId_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("A refund request with this idempotency key already exists for this payment.");
            }
            if (innerMsg.Contains("IX_Refunds_RefundReference", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("A refund with this reference already exists.");
            }
            throw new ConflictException("Refund record could not be created due to a concurrent conflict.");
        }
    }

    public async Task<Refund?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .Include(r => r.Order)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Refund?> GetByIdempotencyKeyAsync(int paymentId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId && r.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<Refund>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Refund>> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.Refunds
            .AsNoTracking()
            .Where(r => r.PaymentId == paymentId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Refund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);
        _context.Refunds.Update(refund);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Refund> ProcessSuccessfulRefundAsync(
        int refundId,
        string refundReference,
        CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var refund = await _context.Refunds
                .FromSql($"SELECT * FROM Refunds WITH (UPDLOCK, ROWLOCK) WHERE Id = {refundId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (refund == null)
            {
                throw new NotFoundException($"Refund with ID {refundId} was not found.");
            }

            if (refund.Status == RefundStatus.Success)
            {
                await transaction.CommitAsync(cancellationToken);
                return refund;
            }

            if (refund.Amount <= 0)
            {
                throw new ValidationException("Refund amount must be greater than zero.");
            }

            if (refund.Currency != "INR")
            {
                throw new ValidationException("Refund currency must match captured payment currency (INR).");
            }

            var payment = await _context.Payments
                .FromSql($"SELECT * FROM Payments WITH (UPDLOCK, ROWLOCK) WHERE Id = {refund.PaymentId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
            {
                throw new NotFoundException($"Payment with ID {refund.PaymentId} was not found.");
            }

            var order = await _context.Orders
                .FromSql($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = {refund.OrderId}")
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
            {
                throw new NotFoundException($"Order with ID {refund.OrderId} was not found.");
            }

            // 1. Financial integrity: Calculate total successful refunds excluding this one
            var otherRefunded = await _context.Refunds
                .Where(r => r.PaymentId == payment.Id && r.Id != refund.Id && r.Status == RefundStatus.Success)
                .SumAsync(r => (decimal?)r.Amount, cancellationToken) ?? 0m;

            if (otherRefunded >= payment.Amount)
            {
                throw new ConflictException("Payment has already been fully refunded.");
            }

            var totalRefunded = otherRefunded + refund.Amount;
            if (totalRefunded > payment.Amount)
            {
                throw new ConflictException($"Total successful refunds ({totalRefunded}) cannot exceed captured payment amount ({payment.Amount}).");
            }

            // 2. Update Refund to Success
            refund.Status = RefundStatus.Success;
            refund.RefundReference = refundReference;
            refund.RefundedAtUtc = DateTime.UtcNow;
            refund.FailureReason = null;

            // 3. Mark payment and order as Refunded if full captured amount is reached
            if (totalRefunded >= payment.Amount)
            {
                payment.PaymentStatus = PaymentStatus.Refunded;
                payment.RequiresRefund = false;
                order.PaymentStatus = PaymentStatus.Refunded;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return refund;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
