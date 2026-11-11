using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(int paymentId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdWithOrderAsync(int paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Payment?> GetActiveProcessingPaymentAsync(int orderId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdempotencyKeyAsync(int orderId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<Payment> AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderForPaymentAsync(int orderId, CancellationToken cancellationToken = default);
    Task<(Payment payment, Order order)> ProcessSuccessfulPaymentAsync(int paymentId, string transactionId, CancellationToken cancellationToken = default);
}
