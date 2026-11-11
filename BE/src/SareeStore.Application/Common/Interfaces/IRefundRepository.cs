using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface IRefundRepository
{
    Task<Refund> AddAsync(Refund refund, CancellationToken cancellationToken = default);
    Task<Refund?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Refund?> GetByIdempotencyKeyAsync(int paymentId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Refund>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Refund>> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Refund refund, CancellationToken cancellationToken = default);
    Task<Refund> ProcessSuccessfulRefundAsync(int refundId, string refundReference, CancellationToken cancellationToken = default);
}
