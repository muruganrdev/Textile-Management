using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for Cart and CartItem entities.
/// </summary>
public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<Cart?> GetByUserIdWithDetailsAsync(int userId, CancellationToken cancellationToken = default);
    Task<Cart> CreateAsync(Cart cart, CancellationToken cancellationToken = default);
    Task<CartItem?> GetItemAsync(int cartId, int productId, CancellationToken cancellationToken = default);
    Task AddItemAsync(CartItem item, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(CartItem item, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(CartItem item, CancellationToken cancellationToken = default);
    Task ClearAsync(int cartId, CancellationToken cancellationToken = default);
}
