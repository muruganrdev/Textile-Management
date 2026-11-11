using SareeStore.Application.DTOs.Products;

namespace SareeStore.Application.Services.Interfaces;

public interface IProductService
{
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
