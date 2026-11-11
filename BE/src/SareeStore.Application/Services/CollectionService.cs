using System.Text.RegularExpressions;
using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Collections;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;

namespace SareeStore.Application.Services;

public class CollectionService : ICollectionService
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IValidator<CreateCollectionRequest> _createValidator;
    private readonly IValidator<UpdateCollectionRequest> _updateValidator;

    public CollectionService(
        ICollectionRepository collectionRepository,
        IValidator<CreateCollectionRequest> createValidator,
        IValidator<UpdateCollectionRequest> updateValidator)
    {
        _collectionRepository = collectionRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CollectionResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var collection = await _collectionRepository.GetByIdAsync(id, cancellationToken);
        if (collection == null)
        {
            throw new NotFoundException(nameof(Collection), id);
        }

        return MapToResponse(collection);
    }

    public async Task<IReadOnlyList<CollectionResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var collections = await _collectionRepository.GetAllAsync(includeInactive, cancellationToken);
        return collections.Select(MapToResponse).ToList();
    }

    public async Task<CollectionResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var collection = await _collectionRepository.GetBySlugAsync(slug, cancellationToken);
        if (collection == null)
        {
            throw new NotFoundException(nameof(Collection), slug);
        }

        return MapToResponse(collection);
    }

    public async Task<CollectionResponse> CreateAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var trimmedName = request.Name.Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(trimmedName)
            : GenerateSlug(request.Slug.Trim());

        if (await _collectionRepository.ExistsBySlugAsync(slug, null, cancellationToken))
        {
            throw new ConflictException($"Collection with slug '{slug}' already exists.");
        }

        var collection = new Collection
        {
            Name = trimmedName,
            Slug = slug,
            Description = request.Description?.Trim(),
            BannerImageUrl = request.BannerImageUrl?.Trim(),
            IsFeatured = request.IsFeatured,
            IsActive = request.IsActive
        };

        var created = await _collectionRepository.AddAsync(collection, cancellationToken);
        return MapToResponse(created);
    }

    public async Task<CollectionResponse> UpdateAsync(int id, UpdateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var collection = await _collectionRepository.GetByIdAsync(id, cancellationToken);
        if (collection == null)
        {
            throw new NotFoundException(nameof(Collection), id);
        }

        var trimmedName = request.Name.Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(trimmedName)
            : GenerateSlug(request.Slug.Trim());

        if (await _collectionRepository.ExistsBySlugAsync(slug, id, cancellationToken))
        {
            throw new ConflictException($"Collection with slug '{slug}' already exists.");
        }

        collection.Name = trimmedName;
        collection.Slug = slug;
        collection.Description = request.Description?.Trim();
        collection.BannerImageUrl = request.BannerImageUrl?.Trim();
        collection.IsFeatured = request.IsFeatured;
        collection.IsActive = request.IsActive;

        await _collectionRepository.UpdateAsync(collection, cancellationToken);
        return MapToResponse(collection);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var collection = await _collectionRepository.GetByIdAsync(id, cancellationToken);
        if (collection == null)
        {
            throw new NotFoundException(nameof(Collection), id);
        }

        collection.IsActive = false;
        await _collectionRepository.UpdateAsync(collection, cancellationToken);
    }

    private static CollectionResponse MapToResponse(Collection collection)
    {
        return new CollectionResponse
        {
            Id = collection.Id,
            Name = collection.Name,
            Slug = collection.Slug,
            Description = collection.Description,
            BannerImageUrl = collection.BannerImageUrl,
            IsFeatured = collection.IsFeatured,
            IsActive = collection.IsActive,
            ProductCount = collection.ProductCollections?.Count ?? 0,
            CreatedAtUtc = collection.CreatedAtUtc,
            UpdatedAtUtc = collection.UpdatedAtUtc
        };
    }

    private static string GenerateSlug(string phrase)
    {
        var str = phrase.ToLowerInvariant();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        str = Regex.Replace(str, @"\s+", "-").Trim('-');
        return str;
    }
}
