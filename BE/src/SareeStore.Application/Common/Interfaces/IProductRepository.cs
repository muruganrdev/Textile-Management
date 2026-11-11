using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySkuAsync(string sku, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasOrdersAsync(int id, CancellationToken cancellationToken = default);
    Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
}
