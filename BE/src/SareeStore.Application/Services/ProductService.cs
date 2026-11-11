using System.Text.RegularExpressions;
using FluentValidation;
using SareeStore.Application.Common.Exceptions;
using ValidationException = SareeStore.Application.Common.Exceptions.ValidationException;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Products;
using SareeStore.Application.Services.Interfaces;
using SareeStore.Domain.Entities;

namespace SareeStore.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        return MapToResponse(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var products = await _productRepository.GetAllAsync(includeInactive, cancellationToken);
        return products.Select(MapToResponse).ToList();
    }

    public async Task<IReadOnlyList<ProductResponse>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var categoryExists = await _categoryRepository.ExistsByIdAsync(categoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), categoryId);
        }

        var products = await _productRepository.GetByCategoryIdAsync(categoryId, cancellationToken);
        return products.Select(MapToResponse).ToList();
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var trimmedSku = request.SKU.Trim();
        if (await _productRepository.ExistsBySkuAsync(trimmedSku, null, cancellationToken))
        {
            throw new ConflictException($"Product with SKU '{trimmedSku}' already exists.");
        }

        var categoryExists = await _categoryRepository.ExistsByIdAsync(request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), request.CategoryId);
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug($"{request.Title}-{trimmedSku}")
            : GenerateSlug(request.Slug.Trim());

        if (await _productRepository.ExistsBySlugAsync(slug, null, cancellationToken))
        {
            throw new ConflictException($"Product with slug '{slug}' already exists.");
        }

        var product = new Product
        {
            CategoryId = request.CategoryId,
            SKU = trimmedSku,
            Title = request.Title.Trim(),
            Slug = slug,
            Description = request.Description.Trim(),
            Price = request.Price,
            DiscountPrice = request.DiscountPrice,
            Fabric = request.Fabric.Trim(),
            WeaveType = request.WeaveType.Trim(),
            ZariType = request.ZariType.Trim(),
            PrimaryColor = request.PrimaryColor.Trim(),
            SareeLengthMeters = request.SareeLengthMeters,
            SareeWidthInches = request.SareeWidthInches,
            BlouseIncluded = request.BlouseIncluded,
            BlouseLengthMeters = request.BlouseLengthMeters,
            CraftDetails = request.CraftDetails?.Trim(),
            Occasion = request.Occasion?.Trim(),
            Has3DModel = request.Has3DModel,
            IsActive = request.IsActive,
            Inventory = new Inventory
            {
                StockQuantity = 0,
                LowStockThreshold = 2
            }
        };

        if (request.CollectionIds != null && request.CollectionIds.Count > 0)
        {
            foreach (var collectionId in request.CollectionIds.Distinct())
            {
                product.ProductCollections.Add(new ProductCollection
                {
                    CollectionId = collectionId
                });
            }
        }

        var created = await _productRepository.AddAsync(product, cancellationToken);
        return MapToResponse(created);
    }

    public async Task<ProductResponse> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        var trimmedSku = request.SKU.Trim();
        if (await _productRepository.ExistsBySkuAsync(trimmedSku, id, cancellationToken))
        {
            throw new ConflictException($"Product with SKU '{trimmedSku}' already exists.");
        }

        var categoryExists = await _categoryRepository.ExistsByIdAsync(request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new NotFoundException(nameof(Category), request.CategoryId);
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug($"{request.Title}-{trimmedSku}")
            : GenerateSlug(request.Slug.Trim());

        if (await _productRepository.ExistsBySlugAsync(slug, id, cancellationToken))
        {
            throw new ConflictException($"Product with slug '{slug}' already exists.");
        }

        product.CategoryId = request.CategoryId;
        product.SKU = trimmedSku;
        product.Title = request.Title.Trim();
        product.Slug = slug;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.DiscountPrice = request.DiscountPrice;
        product.Fabric = request.Fabric.Trim();
        product.WeaveType = request.WeaveType.Trim();
        product.ZariType = request.ZariType.Trim();
        product.PrimaryColor = request.PrimaryColor.Trim();
        product.SareeLengthMeters = request.SareeLengthMeters;
        product.SareeWidthInches = request.SareeWidthInches;
        product.BlouseIncluded = request.BlouseIncluded;
        product.BlouseLengthMeters = request.BlouseLengthMeters;
        product.CraftDetails = request.CraftDetails?.Trim();
        product.Occasion = request.Occasion?.Trim();
        product.Has3DModel = request.Has3DModel;
        product.IsActive = request.IsActive;

        if (request.CollectionIds != null)
        {
            product.ProductCollections.Clear();
            foreach (var collectionId in request.CollectionIds.Distinct())
            {
                product.ProductCollections.Add(new ProductCollection
                {
                    ProductId = product.Id,
                    CollectionId = collectionId
                });
            }
        }

        await _productRepository.UpdateAsync(product, cancellationToken);
        return MapToResponse(product);
    }

    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException(nameof(Product), id);
        }

        // Soft deletion / deactivation to preserve historical order integrity
        product.IsActive = false;
        await _productRepository.UpdateAsync(product, cancellationToken);
    }

    private static ProductResponse MapToResponse(Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            SKU = product.SKU,
            Title = product.Title,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            Fabric = product.Fabric,
            WeaveType = product.WeaveType,
            ZariType = product.ZariType,
            PrimaryColor = product.PrimaryColor,
            SareeLengthMeters = product.SareeLengthMeters,
            SareeWidthInches = product.SareeWidthInches,
            BlouseIncluded = product.BlouseIncluded,
            BlouseLengthMeters = product.BlouseLengthMeters,
            CraftDetails = product.CraftDetails,
            Occasion = product.Occasion,
            Has3DModel = product.Has3DModel,
            IsActive = product.IsActive,
            StockQuantity = product.Inventory?.StockQuantity ?? 0,
            PrimaryImageUrl = product.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                ?? product.Images?.FirstOrDefault()?.ImageUrl,
            CollectionIds = product.ProductCollections?.Select(pc => pc.CollectionId).ToList() ?? new List<int>(),
            CreatedAtUtc = product.CreatedAtUtc,
            UpdatedAtUtc = product.UpdatedAtUtc
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
