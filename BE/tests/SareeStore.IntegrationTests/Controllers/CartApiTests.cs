using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class CartApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;
    private readonly HttpClient _client;

    public CartApiTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Cart_FullLifecycle_ShouldSucceedForCustomer()
    {
        // 1. Register customer
        var (token, customerId) = await RegisterCustomerAsync("cart_lifecycle");

        // 2. Seed Product with Inventory
        var product = await SeedProductAsync(stock: 10, price: 5000m, discountPrice: 4200m);

        using var client = CreateAuthenticatedClient(token);

        // 3. GET empty cart
        var emptyCartResponse = await client.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, emptyCartResponse.StatusCode);
        var emptyCart = await emptyCartResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(emptyCart);
        Assert.Empty(emptyCart.Items);
        Assert.Equal(0, emptyCart.TotalItems);
        Assert.Equal(0.00m, emptyCart.SubTotal);

        // 4. POST add item to cart (Qty: 2)
        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var cartAfterAdd = await addResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartAfterAdd);
        Assert.Single(cartAfterAdd.Items);
        Assert.Equal(2, cartAfterAdd.TotalItems);
        Assert.Equal(8400m, cartAfterAdd.SubTotal); // 4200 * 2

        var item = cartAfterAdd.Items[0];
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(product.Title, item.ProductTitle);
        Assert.Equal(4200m, item.EffectiveUnitPrice);
        Assert.True(item.IsAvailable);

        // 5. POST add same item again (Qty: 1) -> should increment to 3
        var addAgainResponse = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 1
        });
        Assert.Equal(HttpStatusCode.OK, addAgainResponse.StatusCode);
        var cartAfterIncrement = await addAgainResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartAfterIncrement);
        Assert.Single(cartAfterIncrement.Items);
        Assert.Equal(3, cartAfterIncrement.TotalItems); // 2 + 1
        Assert.Equal(12600m, cartAfterIncrement.SubTotal); // 4200 * 3

        // 6. PUT update item quantity to 4
        var updateResponse = await client.PutAsJsonAsync($"/api/cart/items/{product.Id}", new UpdateCartItemRequest
        {
            Quantity = 4
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var cartAfterUpdate = await updateResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartAfterUpdate);
        Assert.Equal(4, cartAfterUpdate.TotalItems);
        Assert.Equal(16800m, cartAfterUpdate.SubTotal); // 4200 * 4

        // 7. DELETE remove item
        var removeResponse = await client.DeleteAsync($"/api/cart/items/{product.Id}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        var cartAfterRemove = await removeResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartAfterRemove);
        Assert.Empty(cartAfterRemove.Items);
        Assert.Equal(0, cartAfterRemove.TotalItems);

        // 8. Re-add and clear cart
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 2 });
        var clearResponse = await client.DeleteAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cartAfterClear = await clearResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartAfterClear);
        Assert.Empty(cartAfterClear.Items);
    }

    [Fact]
    public async Task Cart_CrossCustomerIsolation_CustomerACannotAccessOrAffectCustomerBCart()
    {
        // Arrange: Register Customer A and Customer B
        var (tokenA, _) = await RegisterCustomerAsync("cust_a");
        var (tokenB, _) = await RegisterCustomerAsync("cust_b");

        var product = await SeedProductAsync(stock: 5, price: 8000m);

        using var clientA = CreateAuthenticatedClient(tokenA);
        using var clientB = CreateAuthenticatedClient(tokenB);

        // Act: Customer A adds product to cart
        var addResponseA = await clientA.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, addResponseA.StatusCode);

        // Assert: Customer B's cart remains completely empty
        var responseB = await clientB.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        var cartB = await responseB.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cartB);
        Assert.Empty(cartB.Items);
        Assert.Equal(0, cartB.TotalItems);
    }

    [Fact]
    public async Task AddToCart_WhenQuantityExceedsAvailableStock_ShouldReturn400BadRequest()
    {
        // Arrange
        var (token, _) = await RegisterCustomerAsync("stock_limit");
        var product = await SeedProductAsync(stock: 2, price: 3000m);

        using var client = CreateAuthenticatedClient(token);

        // Act: Request 5 units when only 2 available
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 5
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
    }

    [Fact]
    public async Task CartEndpoints_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Act & Assert
        var getResponse = await _client.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);

        var postResponse = await _client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = 1, Quantity = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);

        var putResponse = await _client.PutAsJsonAsync("/api/cart/items/1", new UpdateCartItemRequest { Quantity = 2 });
        Assert.Equal(HttpStatusCode.Unauthorized, putResponse.StatusCode);

        var deleteItemResponse = await _client.DeleteAsync("/api/cart/items/1");
        Assert.Equal(HttpStatusCode.Unauthorized, deleteItemResponse.StatusCode);

        var deleteCartResponse = await _client.DeleteAsync("/api/cart");
        Assert.Equal(HttpStatusCode.Unauthorized, deleteCartResponse.StatusCode);
    }

    [Fact]
    public async Task CartEndpoints_WhenCalledByEmployeeOrSuperAdmin_ShouldReturn403Forbidden()
    {
        // Arrange: Generate tokens for Employee and SuperAdmin
        var employeeToken = GenerateTokenForRole(UserRole.Employee, 901, "employee_cart@test.com");
        var superAdminToken = GenerateTokenForRole(UserRole.SuperAdmin, 902, "superadmin_cart@test.com");

        using var employeeClient = CreateAuthenticatedClient(employeeToken);
        using var superAdminClient = CreateAuthenticatedClient(superAdminToken);

        // Act & Assert for Employee
        var empResponse = await employeeClient.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.Forbidden, empResponse.StatusCode);

        // Act & Assert for SuperAdmin
        var adminResponse = await superAdminClient.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
    }

    private async Task<(string Token, int CustomerId)> RegisterCustomerAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = $"{prefix} User",
            Email = $"{prefix}_{suffix}@sareestore.com",
            Password = "Password123#",
            PhoneNumber = "+919876543210"
        });

        response.EnsureSuccessStatusCode();
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (authResponse!.AccessToken, authResponse.User.Id);
    }

    private async Task<Product> SeedProductAsync(int stock, decimal price, decimal? discountPrice = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = new Category
        {
            Name = $"Cart Category {suffix}",
            Slug = $"cart-cat-{suffix}",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            SKU = $"CART-SKU-{suffix}",
            Title = $"Cart Test Saree {suffix}",
            Slug = $"cart-saree-{suffix}",
            Description = "Test product for cart operations",
            Price = price,
            DiscountPrice = discountPrice,
            Fabric = "Silk",
            WeaveType = "Handloom",
            ZariType = "Zari",
            PrimaryColor = "Red",
            IsActive = true,
            Inventory = new Inventory
            {
                StockQuantity = stock,
                LowStockThreshold = 1
            }
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string GenerateTokenForRole(UserRole role, int userId, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = new User
        {
            Id = userId,
            FullName = $"{role} User",
            Email = email,
            Role = role,
            IsActive = true
        };

        return tokenGenerator.GenerateToken(user);
    }
}
