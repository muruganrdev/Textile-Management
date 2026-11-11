using SareeStore.Application.DTOs.Categories;

namespace SareeStore.Application.Services.Interfaces;

public interface ICategoryService
{
    Task<CategoryResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
