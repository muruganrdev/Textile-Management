using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> HasProductsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetProductCountAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetProductCountsAsync(CancellationToken cancellationToken = default);
    Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);
}
