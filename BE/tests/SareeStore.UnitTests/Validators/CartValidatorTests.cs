using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.Validators;

namespace SareeStore.UnitTests.Validators;

public class CartValidatorTests
{
    private readonly AddToCartRequestValidator _addValidator = new();
    private readonly UpdateCartItemRequestValidator _updateValidator = new();

    [Fact]
    public async Task AddToCartValidator_ValidRequest_ShouldPass()
    {
        var request = new AddToCartRequest { ProductId = 1, Quantity = 2 };
        var result = await _addValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddToCartValidator_InvalidProductId_ShouldFail(int productId)
    {
        var request = new AddToCartRequest { ProductId = productId, Quantity = 1 };
        var result = await _addValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddToCartRequest.ProductId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddToCartValidator_InvalidQuantity_ShouldFail(int quantity)
    {
        var request = new AddToCartRequest { ProductId = 1, Quantity = quantity };
        var result = await _addValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddToCartRequest.Quantity));
    }

    [Fact]
    public async Task UpdateCartItemValidator_ValidRequest_ShouldPass()
    {
        var request = new UpdateCartItemRequest { Quantity = 3 };
        var result = await _updateValidator.ValidateAsync(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task UpdateCartItemValidator_InvalidQuantity_ShouldFail(int quantity)
    {
        var request = new UpdateCartItemRequest { Quantity = quantity };
        var result = await _updateValidator.ValidateAsync(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateCartItemRequest.Quantity));
    }
}
