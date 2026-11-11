using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface ICollectionRepository
{
    Task<Collection?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Collection>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Collection?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<Collection> AddAsync(Collection collection, CancellationToken cancellationToken = default);
    Task UpdateAsync(Collection collection, CancellationToken cancellationToken = default);
}
