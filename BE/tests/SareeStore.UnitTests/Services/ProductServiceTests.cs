using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Products;
using SareeStore.Application.Services;
using SareeStore.Application.Validators;
using SareeStore.Domain.Entities;

namespace SareeStore.UnitTests.Services;

public class ProductServiceTests
{
    private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly CreateProductRequestValidator _createValidator = new();
    private readonly UpdateProductRequestValidator _updateValidator = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(
            _productRepository,
            _categoryRepository,
            _createValidator,
            _updateValidator);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ReturnsProductResponse()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            CategoryId = 1,
            SKU = "KAN-GLD-001",
            Title = "Kanchipuram Pure Gold Zari Silk Saree",
            Description = "Authentic handwoven silk saree from Kanchipuram master weavers.",
            Price = 25000m,
            DiscountPrice = 22000m,
            Fabric = "Mulberry Silk",
            WeaveType = "Handloom Korvai",
            ZariType = "Pure Gold Zari",
            PrimaryColor = "Crimson Red",
            SareeLengthMeters = 5.5m,
            SareeWidthInches = 48.0m,
            BlouseIncluded = true,
            BlouseLengthMeters = 0.8m,
            IsActive = true
        };

        _productRepository.ExistsBySkuAsync(request.SKU, null, Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepository.ExistsByIdAsync(request.CategoryId, Arg.Any<CancellationToken>())
            .Returns(true);
        _productRepository.ExistsBySlugAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        _productRepository.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var p = callInfo.Arg<Product>();
                p.Id = 101;
                return p;
            });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.Id);
        Assert.Equal("KAN-GLD-001", result.SKU);
        Assert.Equal(25000m, result.Price);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateSku_ThrowsConflictException()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            CategoryId = 1,
            SKU = "KAN-GLD-001",
            Title = "Kanchipuram Pure Gold Zari Silk Saree",
            Description = "Authentic handwoven silk saree from Kanchipuram master weavers.",
            Price = 25000m,
            Fabric = "Mulberry Silk",
            WeaveType = "Handloom Korvai",
            ZariType = "Pure Gold Zari",
            PrimaryColor = "Crimson Red",
            SareeLengthMeters = 5.5m,
            SareeWidthInches = 48.0m
        };

        _productRepository.ExistsBySkuAsync("KAN-GLD-001", null, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_WhenCategoryDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            CategoryId = 999,
            SKU = "KAN-GLD-002",
            Title = "Kanchipuram Pure Gold Zari Silk Saree",
            Description = "Authentic handwoven silk saree from Kanchipuram master weavers.",
            Price = 25000m,
            Fabric = "Mulberry Silk",
            WeaveType = "Handloom Korvai",
            ZariType = "Pure Gold Zari",
            PrimaryColor = "Crimson Red",
            SareeLengthMeters = 5.5m,
            SareeWidthInches = 48.0m
        };

        _productRepository.ExistsBySkuAsync("KAN-GLD-002", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepository.ExistsByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task DeactivateAsync_WhenProductExists_SetsIsActiveToFalse()
    {
        // Arrange
        var existingProduct = new Product
        {
            Id = 42,
            Title = "Banarasi Brocade Silk",
            SKU = "BAN-001",
            IsActive = true
        };

        _productRepository.GetByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(existingProduct);

        // Act
        await _service.DeactivateAsync(42);

        // Assert
        Assert.False(existingProduct.IsActive);
        await _productRepository.Received(1).UpdateAsync(existingProduct, Arg.Any<CancellationToken>());
    }
}
