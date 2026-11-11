using Microsoft.EntityFrameworkCore;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Data;

namespace SareeStore.Infrastructure.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly ApplicationDbContext _context;

    public CollectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Collection?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .Include(c => c.ProductCollections)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Collections.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .Include(c => c.ProductCollections)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Collection?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .AsNoTracking()
            .Include(c => c.ProductCollections)
            .FirstOrDefaultAsync(c => c.Slug == slug, cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Collections
            .AnyAsync(c => c.Slug == slug && (!excludeId.HasValue || c.Id != excludeId.Value), cancellationToken);
    }

    public async Task<Collection> AddAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        await _context.Collections.AddAsync(collection, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return collection;
    }

    public async Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(collection).State == EntityState.Detached)
        {
            _context.Collections.Update(collection);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
