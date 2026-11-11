using SareeStore.Application.DTOs.Orders;

namespace SareeStore.Application.Services.Interfaces;

public interface IRefundService
{
    Task<RefundResponse> ProcessPendingRefundAsync(
        int refundId,
        CancellationToken cancellationToken = default);

    Task<RefundResponse> InitiateRefundForOrderCancellationAsync(
        int orderId,
        int paymentId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundResponse>> GetOrderRefundsAsync(
        int customerId,
        int orderId,
        bool isEmployeeOrAdmin,
        CancellationToken cancellationToken = default);

    Task<RefundResponse> RetryOrReconcileRefundAsync(
        int? customerId,
        int refundId,
        CancellationToken cancellationToken = default);

    Task<RefundResponse> RetryOrReconcileRefundAsync(
        int refundId,
        CancellationToken cancellationToken = default);
}
