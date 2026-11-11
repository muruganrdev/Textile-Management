using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Categories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.Name == name && (!excludeId.HasValue || c.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.Slug == slug && (!excludeId.HasValue || c.Id != excludeId.Value), cancellationToken);
    }

    public async Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AnyAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<bool> HasProductsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AnyAsync(p => p.CategoryId == id, cancellationToken);
    }

    public async Task<int> GetProductCountAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .CountAsync(p => p.CategoryId == categoryId && p.IsActive, cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetProductCountsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);
    }

    public async Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(category, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(category).State == EntityState.Detached)
        {
            _context.Categories.Update(category);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
