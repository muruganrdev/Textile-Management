using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.Services;
using SareeStore.Application.Validators;
using SareeStore.Domain.Entities;

namespace SareeStore.UnitTests.Services;

public class CartServiceTests
{
    private readonly ICartRepository _cartRepository = Substitute.For<ICartRepository>();
    private readonly IProductRepository _productRepository = Substitute.For<IProductRepository>();
    private readonly AddToCartRequestValidator _addValidator = new();
    private readonly UpdateCartItemRequestValidator _updateValidator = new();
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        _cartService = new CartService(
            _cartRepository,
            _productRepository,
            _addValidator,
            _updateValidator);
    }

    [Fact]
    public async Task GetCartAsync_WhenCartDoesNotExist_ShouldReturnEmptyCartResponse()
    {
        // Arrange
        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns((Cart?)null);

        // Act
        var response = await _cartService.GetCartAsync(1);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(0, response.CartId);
        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalItems);
        Assert.Equal(0.00m, response.SubTotal);
    }

    [Fact]
    public async Task GetCartAsync_WhenCartHasItems_ShouldCalculateEffectivePricesAndTotalsCorrectly()
    {
        // Arrange
        var cart = new Cart
        {
            Id = 10,
            UserId = 1,
            Items = new List<CartItem>
            {
                new CartItem
                {
                    CartId = 10,
                    ProductId = 101,
                    Quantity = 2,
                    Product = new Product
                    {
                        Id = 101,
                        Title = "Kanchipuram Silk",
                        SKU = "KAN-001",
                        Price = 10000m,
                        DiscountPrice = 8500m, // Discounted
                        IsActive = true,
                        Inventory = new Inventory { StockQuantity = 5 },
                        Images = new List<ProductImage> { new ProductImage { ImageUrl = "https://img.com/1.jpg", IsPrimary = true } }
                    }
                },
                new CartItem
                {
                    CartId = 10,
                    ProductId = 102,
                    Quantity = 1,
                    Product = new Product
                    {
                        Id = 102,
                        Title = "Banarasi Brocade",
                        SKU = "BAN-002",
                        Price = 15000m,
                        DiscountPrice = null, // Regular price
                        IsActive = true,
                        Inventory = new Inventory { StockQuantity = 3 }
                    }
                }
            }
        };

        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(cart);

        // Act
        var response = await _cartService.GetCartAsync(1);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(10, response.CartId);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(3, response.TotalItems); // 2 + 1

        var item1 = response.Items.First(i => i.ProductId == 101);
        Assert.Equal(8500m, item1.EffectiveUnitPrice);
        Assert.Equal(17000m, item1.LineTotal);
        Assert.True(item1.IsAvailable);
        Assert.Null(item1.AvailabilityMessage);
        Assert.Equal("https://img.com/1.jpg", item1.PrimaryImageUrl);

        var item2 = response.Items.First(i => i.ProductId == 102);
        Assert.Equal(15000m, item2.EffectiveUnitPrice);
        Assert.Equal(15000m, item2.LineTotal);

        Assert.Equal(32000m, response.SubTotal); // 17000 + 15000
    }

    [Fact]
    public async Task AddItemAsync_WhenCartDoesNotExist_ShouldCreateCartAndAddItem()
    {
        // Arrange
        var request = new AddToCartRequest { ProductId = 201, Quantity = 2 };
        var product = new Product
        {
            Id = 201,
            Title = "Chanderi Saree",
            Price = 5000m,
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 10 }
        };

        _productRepository.GetByIdAsync(201, Arg.Any<CancellationToken>())
            .Returns(product);

        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>())
            .Returns((Cart?)null);

        var createdCart = new Cart { Id = 77, UserId = 1 };
        _cartRepository.CreateAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>())
            .Returns(createdCart);

        _cartRepository.GetItemAsync(77, 201, Arg.Any<CancellationToken>())
            .Returns((CartItem?)null);

        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Cart
            {
                Id = 77,
                UserId = 1,
                Items = new List<CartItem>
                {
                    new CartItem { CartId = 77, ProductId = 201, Quantity = 2, Product = product }
                }
            });

        // Act
        var response = await _cartService.AddItemAsync(1, request);

        // Assert
        Assert.NotNull(response);
        await _cartRepository.Received(1).CreateAsync(Arg.Is<Cart>(c => c.UserId == 1), Arg.Any<CancellationToken>());
        await _cartRepository.Received(1).AddItemAsync(
            Arg.Is<CartItem>(ci => ci.CartId == 77 && ci.ProductId == 201 && ci.Quantity == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItemAsync_WhenItemAlreadyInCart_ShouldIncrementQuantity()
    {
        // Arrange
        var request = new AddToCartRequest { ProductId = 301, Quantity = 2 };
        var product = new Product
        {
            Id = 301,
            Title = "Tussar Silk",
            Price = 7500m,
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 10 }
        };

        var existingCart = new Cart { Id = 5, UserId = 1 };
        var existingItem = new CartItem { Id = 9, CartId = 5, ProductId = 301, Quantity = 3 };

        _productRepository.GetByIdAsync(301, Arg.Any<CancellationToken>())
            .Returns(product);
        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(existingCart);
        _cartRepository.GetItemAsync(5, 301, Arg.Any<CancellationToken>())
            .Returns(existingItem);

        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Cart
            {
                Id = 5,
                UserId = 1,
                Items = new List<CartItem>
                {
                    new CartItem { CartId = 5, ProductId = 301, Quantity = 5, Product = product }
                }
            });

        // Act
        var response = await _cartService.AddItemAsync(1, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(5, existingItem.Quantity); // 3 + 2
        await _cartRepository.Received(1).UpdateItemAsync(existingItem, Arg.Any<CancellationToken>());
        await _cartRepository.DidNotReceive().AddItemAsync(Arg.Any<CartItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItemAsync_WhenProductIsInactive_ShouldThrowValidationException()
    {
        // Arrange
        var request = new AddToCartRequest { ProductId = 401, Quantity = 1 };
        var inactiveProduct = new Product
        {
            Id = 401,
            Title = "Archived Saree",
            IsActive = false,
            Inventory = new Inventory { StockQuantity = 10 }
        };

        _productRepository.GetByIdAsync(401, Arg.Any<CancellationToken>())
            .Returns(inactiveProduct);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _cartService.AddItemAsync(1, request));
        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
        await _cartRepository.DidNotReceive().AddItemAsync(Arg.Any<CartItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddItemAsync_WhenProductIsOutOfStock_ShouldThrowValidationException()
    {
        // Arrange
        var request = new AddToCartRequest { ProductId = 501, Quantity = 1 };
        var outOfStockProduct = new Product
        {
            Id = 501,
            Title = "Sold Out Saree",
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 0 }
        };

        _productRepository.GetByIdAsync(501, Arg.Any<CancellationToken>())
            .Returns(outOfStockProduct);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _cartService.AddItemAsync(1, request));
        Assert.Contains("out-of-stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddItemAsync_WhenRequestedQuantityExceedsAvailableStock_ShouldThrowValidationException()
    {
        // Arrange
        var request = new AddToCartRequest { ProductId = 601, Quantity = 5 };
        var product = new Product
        {
            Id = 601,
            Title = "Limited Saree",
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 3 } // Only 3 available
        };

        _productRepository.GetByIdAsync(601, Arg.Any<CancellationToken>())
            .Returns(product);
        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Cart { Id = 1, UserId = 1 });
        _cartRepository.GetItemAsync(1, 601, Arg.Any<CancellationToken>())
            .Returns((CartItem?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _cartService.AddItemAsync(1, request));
        Assert.Contains("exceeds available stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_WithValidQuantity_ShouldUpdateAndReturnCart()
    {
        // Arrange
        var request = new UpdateCartItemRequest { Quantity = 3 };
        var product = new Product
        {
            Id = 701,
            Title = "Paithani Saree",
            Price = 12000m,
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 5 }
        };

        var cart = new Cart { Id = 8, UserId = 1 };
        var item = new CartItem { Id = 22, CartId = 8, ProductId = 701, Quantity = 1 };

        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>()).Returns(cart);
        _cartRepository.GetItemAsync(8, 701, Arg.Any<CancellationToken>()).Returns(item);
        _productRepository.GetByIdAsync(701, Arg.Any<CancellationToken>()).Returns(product);

        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Cart
            {
                Id = 8,
                UserId = 1,
                Items = new List<CartItem>
                {
                    new CartItem { CartId = 8, ProductId = 701, Quantity = 3, Product = product }
                }
            });

        // Act
        var response = await _cartService.UpdateItemQuantityAsync(1, 701, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(3, item.Quantity);
        await _cartRepository.Received(1).UpdateItemAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_WhenQuantityExceedsStock_ShouldThrowValidationException()
    {
        // Arrange
        var request = new UpdateCartItemRequest { Quantity = 10 };
        var product = new Product
        {
            Id = 801,
            Title = "Silk Saree",
            IsActive = true,
            Inventory = new Inventory { StockQuantity = 4 }
        };

        var cart = new Cart { Id = 9, UserId = 1 };
        var item = new CartItem { Id = 23, CartId = 9, ProductId = 801, Quantity = 2 };

        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>()).Returns(cart);
        _cartRepository.GetItemAsync(9, 801, Arg.Any<CancellationToken>()).Returns(item);
        _productRepository.GetByIdAsync(801, Arg.Any<CancellationToken>()).Returns(product);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _cartService.UpdateItemQuantityAsync(1, 801, request));
        Assert.Contains("exceeds available stock", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemExists_ShouldRemoveItemFromCart()
    {
        // Arrange
        var cart = new Cart { Id = 12, UserId = 1 };
        var item = new CartItem { Id = 33, CartId = 12, ProductId = 901, Quantity = 1 };

        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>()).Returns(cart);
        _cartRepository.GetItemAsync(12, 901, Arg.Any<CancellationToken>()).Returns(item);
        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(new Cart { Id = 12, UserId = 1, Items = new List<CartItem>() });

        // Act
        var response = await _cartService.RemoveItemAsync(1, 901);

        // Assert
        Assert.NotNull(response);
        await _cartRepository.Received(1).RemoveItemAsync(item, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearCartAsync_WhenCartExists_ShouldCallClearOnRepository()
    {
        // Arrange
        var cart = new Cart { Id = 15, UserId = 1 };

        _cartRepository.GetByUserIdAsync(1, Arg.Any<CancellationToken>()).Returns(cart);
        _cartRepository.GetByUserIdWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(new Cart { Id = 15, UserId = 1, Items = new List<CartItem>() });

        // Act
        var response = await _cartService.ClearCartAsync(1);

        // Assert
        Assert.NotNull(response);
        await _cartRepository.Received(1).ClearAsync(15, Arg.Any<CancellationToken>());
    }
}
