using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .Include(p => p.Images)
            .Include(p => p.ProductCollections)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .Include(p => p.Images)
            .Include(p => p.ProductCollections)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .Include(p => p.Images)
            .Include(p => p.ProductCollections)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Category)
            .Include(p => p.Inventory)
            .Include(p => p.Images)
            .Include(p => p.ProductCollections)
            .FirstOrDefaultAsync(p => p.SKU == sku, cancellationToken);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(p => p.SKU == sku && (!excludeId.HasValue || p.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(p => p.Slug == slug && (!excludeId.HasValue || p.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> HasOrdersAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.OrderItems
            .AnyAsync(oi => oi.ProductId == id, cancellationToken);
    }

    public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Ensure Category reference is loaded so mapped responses include Category.Name
        if (product.Category == null && product.CategoryId > 0)
        {
            await _context.Entry(product).Reference(p => p.Category).LoadAsync(cancellationToken);
        }

        return product;
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(product).State == EntityState.Detached)
        {
            _context.Products.Update(product);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
