using SareeStore.Application.DTOs.Cart;

namespace SareeStore.Application.Services.Interfaces;

/// <summary>
/// Application service contract for shopping cart operations.
/// </summary>
public interface ICartService
{
    Task<CartResponse> GetCartAsync(int userId, CancellationToken cancellationToken = default);
    Task<CartResponse> AddItemAsync(int userId, AddToCartRequest request, CancellationToken cancellationToken = default);
    Task<CartResponse> UpdateItemQuantityAsync(int userId, int productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartResponse> RemoveItemAsync(int userId, int productId, CancellationToken cancellationToken = default);
    Task<CartResponse> ClearCartAsync(int userId, CancellationToken cancellationToken = default);
}
