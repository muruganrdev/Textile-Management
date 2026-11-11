using System.Text.RegularExpressions;
using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Categories;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;

namespace SareeStore.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IValidator<CreateCategoryRequest> _createValidator;
    private readonly IValidator<UpdateCategoryRequest> _updateValidator;

    public CategoryService(
        ICategoryRepository categoryRepository,
        IValidator<CreateCategoryRequest> createValidator,
        IValidator<UpdateCategoryRequest> updateValidator)
    {
        _categoryRepository = categoryRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CategoryResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        var productCount = await _categoryRepository.GetProductCountAsync(category.Id, cancellationToken);
        return MapToResponse(category, productCount);
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(includeInactive, cancellationToken);
        var counts = await _categoryRepository.GetProductCountsAsync(cancellationToken);

        return categories
            .Select(c => MapToResponse(c, counts.GetValueOrDefault(c.Id, 0)))
            .ToList();
    }

    public async Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetBySlugAsync(slug, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), slug);
        }

        var productCount = await _categoryRepository.GetProductCountAsync(category.Id, cancellationToken);
        return MapToResponse(category, productCount);
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var trimmedName = request.Name.Trim();
        if (await _categoryRepository.ExistsByNameAsync(trimmedName, null, cancellationToken))
        {
            throw new ConflictException($"Category with name '{trimmedName}' already exists.");
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(trimmedName)
            : GenerateSlug(request.Slug.Trim());

        if (await _categoryRepository.ExistsBySlugAsync(slug, null, cancellationToken))
        {
            throw new ConflictException($"Category with slug '{slug}' already exists.");
        }

        var category = new Category
        {
            Name = trimmedName,
            Slug = slug,
            Description = request.Description?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = request.IsActive
        };

        var created = await _categoryRepository.AddAsync(category, cancellationToken);
        return MapToResponse(created, 0);
    }

    public async Task<CategoryResponse> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        var trimmedName = request.Name.Trim();
        if (await _categoryRepository.ExistsByNameAsync(trimmedName, id, cancellationToken))
        {
            throw new ConflictException($"Category with name '{trimmedName}' already exists.");
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(trimmedName)
            : GenerateSlug(request.Slug.Trim());

        if (await _categoryRepository.ExistsBySlugAsync(slug, id, cancellationToken))
        {
            throw new ConflictException($"Category with slug '{slug}' already exists.");
        }

        category.Name = trimmedName;
        category.Slug = slug;
        category.Description = request.Description?.Trim();
        category.ImageUrl = request.ImageUrl?.Trim();
        category.IsActive = request.IsActive;

        await _categoryRepository.UpdateAsync(category, cancellationToken);
        var productCount = await _categoryRepository.GetProductCountAsync(category.Id, cancellationToken);
        return MapToResponse(category, productCount);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            throw new NotFoundException(nameof(Category), id);
        }

        category.IsActive = false;
        await _categoryRepository.UpdateAsync(category, cancellationToken);
    }

    private static CategoryResponse MapToResponse(Category category, int productCount)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ImageUrl = category.ImageUrl,
            IsActive = category.IsActive,
            ProductCount = productCount,
            CreatedAtUtc = category.CreatedAtUtc,
            UpdatedAtUtc = category.UpdatedAtUtc
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
