using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for Cart and CartItem persistence.
/// </summary>
public class CartRepository : ICartRepository
{
    private readonly ApplicationDbContext _context;

    public CartRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Cart?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public async Task<Cart?> GetByUserIdWithDetailsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Inventory)
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }

    public async Task<Cart> CreateAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        await _context.Carts.AddAsync(cart, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return cart;
    }

    public async Task<CartItem?> GetItemAsync(int cartId, int productId, CancellationToken cancellationToken = default)
    {
        return await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId, cancellationToken);
    }

    public async Task AddItemAsync(CartItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await _context.CartItems.AddAsync(item, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateItemAsync(CartItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_context.Entry(item).State == EntityState.Detached)
        {
            _context.CartItems.Update(item);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(CartItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAsync(int cartId, CancellationToken cancellationToken = default)
    {
        var items = await _context.CartItems
            .Where(ci => ci.CartId == cartId)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
