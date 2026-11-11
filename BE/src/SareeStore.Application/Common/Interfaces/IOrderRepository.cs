using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<Order> CreateOrderWithCartClearingAsync(Order order, int cartId, CancellationToken cancellationToken = default);
    Task<Order> CreateOrderWithCartClearingAsync(Order order, int cartId, Func<string>? orderNumberGenerator, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAndCustomerIdAsync(int orderId, int customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
    Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<Address?> GetCustomerAddressAsync(int addressId, int customerId, CancellationToken cancellationToken = default);
    Task<Order> CancelOrderWithStockRestorationAsync(int orderId, int? customerId, int? adminUserId, string reason, CancellationToken cancellationToken = default);
}
