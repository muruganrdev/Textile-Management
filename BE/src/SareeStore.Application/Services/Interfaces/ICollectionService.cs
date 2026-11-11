using SareeStore.Application.DTOs.Collections;

namespace SareeStore.Application.Services.Interfaces;

public interface ICollectionService
{
    Task<CollectionResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CollectionResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CollectionResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<CollectionResponse> CreateAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default);
    Task<CollectionResponse> UpdateAsync(int id, UpdateCollectionRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
