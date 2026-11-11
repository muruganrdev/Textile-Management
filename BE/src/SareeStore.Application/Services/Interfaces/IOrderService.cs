using SareeStore.Application.DTOs.Orders;

namespace SareeStore.Application.Services.Interfaces;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(int customerId, CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderResponse>> GetMyOrdersAsync(int customerId, CancellationToken cancellationToken = default);
    Task<OrderResponse> GetOrderByIdAsync(int orderId, int customerId, CancellationToken cancellationToken = default);
    Task<CancelOrderResponse> CancelOrderAsync(int customerId, int orderId, CancelOrderRequest request, CancellationToken cancellationToken = default);
    Task<CancelOrderResponse> AdminCancelOrderAsync(int adminUserId, int orderId, CancelOrderRequest request, CancellationToken cancellationToken = default);
}
