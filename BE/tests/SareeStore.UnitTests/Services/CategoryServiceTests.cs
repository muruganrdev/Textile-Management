using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Categories;
using SareeStore.Application.Services;
using SareeStore.Application.Validators;
using SareeStore.Domain.Entities;

namespace SareeStore.UnitTests.Services;

public class CategoryServiceTests
{
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly CreateCategoryRequestValidator _createValidator = new();
    private readonly UpdateCategoryRequestValidator _updateValidator = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(
            _categoryRepository,
            _createValidator,
            _updateValidator);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_AutoGeneratesSlugAndReturnsResponse()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Kanchipuram Silk",
            Description = "Traditional handwoven sarees from Tamil Nadu."
        };

        _categoryRepository.ExistsByNameAsync("Kanchipuram Silk", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepository.ExistsBySlugAsync("kanchipuram-silk", null, Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepository.AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var c = callInfo.Arg<Category>();
                c.Id = 10;
                return c;
            });

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal("Kanchipuram Silk", result.Name);
        Assert.Equal("kanchipuram-silk", result.Slug);
    }

    [Fact]
    public async Task CreateAsync_WhenDuplicateName_ThrowsConflictException()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Kanchipuram Silk"
        };

        _categoryRepository.ExistsByNameAsync("Kanchipuram Silk", null, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_UsesEfficientCountQuery()
    {
        // Arrange
        var category = new Category
        {
            Id = 5,
            Name = "Banarasi Silk",
            Slug = "banarasi-silk",
            IsActive = true
        };

        _categoryRepository.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(category);
        _categoryRepository.GetProductCountAsync(5, Arg.Any<CancellationToken>())
            .Returns(14);

        // Act
        var result = await _service.GetByIdAsync(5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        Assert.Equal("Banarasi Silk", result.Name);
        Assert.Equal(14, result.ProductCount);
        await _categoryRepository.Received(1).GetProductCountAsync(5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeactivateAsync_DoesNotDeactivateProducts()
    {
        // Arrange
        var category = new Category
        {
            Id = 8,
            Name = "Chanderi Cotton",
            Slug = "chanderi-cotton",
            IsActive = true
        };

        _categoryRepository.GetByIdAsync(8, Arg.Any<CancellationToken>())
            .Returns(category);

        // Act
        await _service.DeactivateAsync(8);

        // Assert
        Assert.False(category.IsActive);
        await _categoryRepository.Received(1).UpdateAsync(category, Arg.Any<CancellationToken>());
    }
}
