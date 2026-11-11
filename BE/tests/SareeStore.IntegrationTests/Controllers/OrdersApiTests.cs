using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class OrdersApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Checkout_FullLifecycle_ShouldCreateOrderPersistSnapshotsAndClearCart()
    {
        // 1. Register customer
        var (token, customerId) = await RegisterCustomerAsync("checkout_lifecycle");
        using var client = CreateAuthenticatedClient(token);

        // 2. Seed address for customer
        var address = await SeedAddressAsync(customerId);

        // 3. Seed product with stock 10, price 8000, discountPrice 6500
        var product = await SeedProductAsync(stock: 10, price: 8000m, discountPrice: 6500m);

        // 4. Add product to cart (Quantity = 2)
        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 2
        });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        // 5. Checkout
        var checkoutResponse = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        var order = await checkoutResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.True(order.Id > 0);
        Assert.StartsWith("ORD-", order.OrderNumber);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(13000m, order.SubTotal); // 6500 * 2
        Assert.Equal(0.00m, order.DiscountAmount);
        Assert.Equal(0.00m, order.ShippingFee);
        Assert.Equal(13000m, order.TotalAmount);
        Assert.Equal("Placed", order.OrderStatus);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Equal("COD", order.PaymentMethod);

        // Address snapshots frozen
        Assert.Equal(address.RecipientName, order.ShippingRecipientName);
        Assert.Equal(address.PhoneNumber, order.ShippingPhoneNumber);
        Assert.Equal(address.AddressLine1, order.ShippingAddressLine1);
        Assert.Equal(address.City, order.ShippingCity);
        Assert.Equal(address.State, order.ShippingState);
        Assert.Equal(address.PostalCode, order.ShippingPostalCode);

        // Order item snapshots frozen
        Assert.Single(order.Items);
        var orderItem = order.Items[0];
        Assert.Equal(product.Id, orderItem.ProductId);
        Assert.Equal(product.Title, orderItem.ProductTitle);
        Assert.Equal(8000m, orderItem.RegularPrice);
        Assert.Equal(6500m, orderItem.UnitPrice);
        Assert.Equal(1500m, orderItem.DiscountAmount);
        Assert.Equal(2, orderItem.Quantity);
        Assert.Equal(13000m, orderItem.LineTotal);

        // 6. Verify cart is now completely empty
        var cartResponse = await client.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cart);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.TotalItems);
        Assert.Equal(0.00m, cart.SubTotal);

        // 7. Verify GET /api/orders/{id}
        var getOrderResponse = await client.GetAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, getOrderResponse.StatusCode);
        var fetchedOrder = await getOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(fetchedOrder);
        Assert.Equal(order.OrderNumber, fetchedOrder.OrderNumber);
        Assert.Equal(order.TotalAmount, fetchedOrder.TotalAmount);

        // 8. Verify GET /api/orders/my-orders
        var myOrdersResponse = await client.GetAsync("/api/orders/my-orders");
        Assert.Equal(HttpStatusCode.OK, myOrdersResponse.StatusCode);
        var myOrders = await myOrdersResponse.Content.ReadFromJsonAsync<List<OrderResponse>>();
        Assert.NotNull(myOrders);
        Assert.Contains(myOrders, o => o.Id == order.Id);

        // 9. Verify stock was deducted atomically from 10 to 8 in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inventory = await db.Inventories.FirstAsync(i => i.ProductId == product.Id);
            Assert.Equal(8, inventory.StockQuantity);

            // Verify initial OrderStatusHistory has FromStatus null and ToStatus Placed
            var history = await db.OrderStatusHistories.FirstAsync(h => h.OrderId == order.Id);
            Assert.Null(history.FromStatus);
            Assert.Equal(OrderStatus.Placed, history.ToStatus);
        }
    }

    [Fact]
    public async Task Checkout_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = 1,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WhenCalledByEmployee_ShouldReturn403Forbidden()
    {
        var token = GenerateTokenForRole(UserRole.Employee, 9991, "employee_order@sareestore.com");
        using var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = 1,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WhenCartIsEmpty_ShouldReturn400BadRequest()
    {
        var (token, customerId) = await RegisterCustomerAsync("empty_cart_checkout");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);

        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WhenAddressBelongsToAnotherCustomer_ShouldReturn400BadRequest()
    {
        // Customer A
        var (tokenA, customerAId) = await RegisterCustomerAsync("customer_a");
        var addressA = await SeedAddressAsync(customerAId);

        // Customer B
        var (tokenB, customerBId) = await RegisterCustomerAsync("customer_b");
        using var clientB = CreateAuthenticatedClient(tokenB);

        var product = await SeedProductAsync(stock: 5, price: 5000m);
        await clientB.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });

        // Customer B attempts to checkout using Customer A's address
        var response = await clientB.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = addressA.Id,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WhenStockIsInsufficient_ShouldReturn400BadRequestAndKeepCartIntact()
    {
        var (token, customerId) = await RegisterCustomerAsync("insufficient_stock_checkout");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);

        // Product with 2 items in stock
        var product = await SeedProductAsync(stock: 2, price: 4000m);
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 2 });

        // Decrease stock in DB to 1 before checkout
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inv = await db.Inventories.FirstAsync(i => i.ProductId == product.Id);
            inv.StockQuantity = 1;
            await db.SaveChangesAsync();
        }

        // Attempt checkout for 2 items
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Cart items should remain intact
        var cartResponse = await client.GetAsync("/api/cart");
        Assert.Equal(HttpStatusCode.OK, cartResponse.StatusCode);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();
        Assert.NotNull(cart);
        Assert.Single(cart.Items);
    }

    [Fact]
    public async Task GetOrderById_CustomerIsolation_CustomerCannotAccessAnotherCustomerOrder()
    {
        // 1. Customer A creates order
        var (tokenA, customerAId) = await RegisterCustomerAsync("isolation_cust_a");
        using var clientA = CreateAuthenticatedClient(tokenA);
        var addressA = await SeedAddressAsync(customerAId);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await clientA.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var orderResponse = await clientA.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = addressA.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 2. Customer B attempts to access Customer A's order
        var (tokenB, _) = await RegisterCustomerAsync("isolation_cust_b");
        using var clientB = CreateAuthenticatedClient(tokenB);

        var response = await clientB.GetAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_Concurrency_TwoCustomersCompetingForLastItem_ExactlyOneSucceedsAndStockNeverNegative()
    {
        // 1. Seed exclusive saree with stock = 1
        var product = await SeedProductAsync(stock: 1, price: 25000m);

        // 2. Customer 1 setup
        var (token1, customer1Id) = await RegisterCustomerAsync("compete_cust_1");
        using var client1 = CreateAuthenticatedClient(token1);
        var address1 = await SeedAddressAsync(customer1Id);
        await client1.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });

        // 3. Customer 2 setup
        var (token2, customer2Id) = await RegisterCustomerAsync("compete_cust_2");
        using var client2 = CreateAuthenticatedClient(token2);
        var address2 = await SeedAddressAsync(customer2Id);
        await client2.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });

        // 4. Concurrent checkouts for the single available item
        var task1 = client1.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address1.Id,
            PaymentMethod = PaymentMethod.COD
        });

        var task2 = client2.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address2.Id,
            PaymentMethod = PaymentMethod.COD
        });

        var responses = await Task.WhenAll(task1, task2);

        // 5. Assert: exactly one 201 Created and one 409 Conflict (or 400 Insufficient)
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.True(statusCodes.Contains(HttpStatusCode.Conflict) || statusCodes.Contains(HttpStatusCode.BadRequest),
            $"Expected Conflict or BadRequest for one request, got: {string.Join(", ", statusCodes)}");

        // 6. Assert stock in database is exactly 0 (NEVER negative!)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var inventory = await db.Inventories.FirstAsync(i => i.ProductId == product.Id);
            Assert.Equal(0, inventory.StockQuantity);

            // Exactly one OrderItem exists for this product
            var orderedCount = await db.OrderItems.CountAsync(oi => oi.ProductId == product.Id);
            Assert.Equal(1, orderedCount);
        }
    }

    [Fact]
    public async Task Checkout_Concurrency_SameCustomerDoubleSubmit_ExactlyOneOrderCreatedAndSecondFailsWith409()
    {
        // 1. Setup single customer with item in cart
        var (token, customerId) = await RegisterCustomerAsync("double_submit_cust");
        using var client1 = CreateAuthenticatedClient(token);
        using var client2 = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 10, price: 9000m);

        await client1.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 2 });

        // 2. Submit concurrent duplicate checkout requests
        var task1 = client1.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });

        var task2 = client2.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });

        var responses = await Task.WhenAll(task1, task2);

        // 3. Assert: exactly one request succeeded (201 Created) and the duplicate was rejected with 409 Conflict
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        var contents = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync()));
        Assert.True(statusCodes.Contains(HttpStatusCode.Conflict), $"Codes: {string.Join(", ", statusCodes)}. Bodies: {string.Join(" | ", contents)}");
        Assert.Single(statusCodes, HttpStatusCode.Created);
        Assert.Single(statusCodes, HttpStatusCode.Conflict);

        // 4. Assert only 1 order was created in DB for this customer
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var customerOrders = await db.Orders.Where(o => o.CustomerId == customerId).ToListAsync();
            Assert.Single(customerOrders);

            // Stock was deducted only once (10 - 2 = 8)
            var inventory = await db.Inventories.FirstAsync(i => i.ProductId == product.Id);
            Assert.Equal(8, inventory.StockQuantity);
        }
    }

    private async Task<(string token, int customerId)> RegisterCustomerAsync(string prefix)
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

    private async Task<Address> SeedAddressAsync(int customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var address = new Address
        {
            UserId = customerId,
            RecipientName = "Test Recipient",
            PhoneNumber = "+919876543210",
            AddressLine1 = "123 Heritage Lane",
            AddressLine2 = "Apt 4B",
            City = "Kanchipuram",
            State = "Tamil Nadu",
            PostalCode = "631501",
            Country = "India",
            AddressType = AddressType.Shipping,
            IsDefault = true
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync();

        return address;
    }

    private async Task<Product> SeedProductAsync(int stock, decimal price, decimal? discountPrice = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var category = new Category
        {
            Name = $"Order Category {suffix}",
            Slug = $"order-cat-{suffix}",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            SKU = $"ORD-SKU-{suffix}",
            Title = $"Order Test Saree {suffix}",
            Slug = $"order-saree-{suffix}",
            Description = "Test saree for order tests",
            Price = price,
            DiscountPrice = discountPrice,
            Fabric = "Silk",
            WeaveType = "Handloom",
            ZariType = "Pure Zari",
            PrimaryColor = "Maroon",
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
