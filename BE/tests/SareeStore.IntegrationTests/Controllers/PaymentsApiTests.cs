using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.DTOs.Cart;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.DTOs.Payment;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Data;
using SareeStore.Infrastructure.PaymentProviders;
using Xunit;

namespace SareeStore.IntegrationTests.Controllers;

[Collection("DatabaseTests")]
public class PaymentsApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;
    private readonly HttpClient _client;

    public PaymentsApiTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Payment_FullLifecycle_InitiateVerifyAndCheckHistory()
    {
        // 1. Register customer and create order
        var (token, customerId) = await RegisterCustomerAsync("payment_lifecycle");
        using var client = CreateAuthenticatedClient(token);

        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 10, price: 5000m);

        // Add to cart
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest
        {
            ProductId = product.Id,
            Quantity = 1
        });

        // Checkout with online payment method (e.g. UPI)
        var checkoutResponse = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        var order = await checkoutResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 2. Initiate Online Payment (pure customer request without simulation parameters)
        var initiateResponse = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "PAY-INIT-001"
        });

        Assert.Equal(HttpStatusCode.Created, initiateResponse.StatusCode);
        var initResult = await initiateResponse.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initResult);
        Assert.True(initResult.PaymentId > 0);
        Assert.Equal(order.Id, initResult.OrderId);
        Assert.Equal("Processing", initResult.Status);
        Assert.StartsWith("SIM-", initResult.PaymentReference);

        // 3. Verify Payment
        var verifyResponse = await client.PostAsJsonAsync($"/api/payments/{initResult.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initResult.PaymentReference
        });

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(verifyResult);
        Assert.Equal("Paid", verifyResult.Status);
        Assert.Equal("Confirmed", verifyResult.OrderStatus);
        Assert.NotNull(verifyResult.PaidAtUtc);
        Assert.False(verifyResult.IsLateSuccess);
        Assert.False(verifyResult.RequiresRefund);

        // 4. Check Order in DB: OrderStatus == Confirmed, PaymentStatus == Paid, History added
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders
                .Include(o => o.StatusHistories)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            Assert.NotNull(dbOrder);
            Assert.Equal(OrderStatus.Confirmed, dbOrder.OrderStatus);
            Assert.Equal(PaymentStatus.Paid, dbOrder.PaymentStatus);

            // History should contain: null -> Placed (from checkout), Placed -> Confirmed (from payment verification)
            var confirmationHistory = dbOrder.StatusHistories.FirstOrDefault(h => h.ToStatus == OrderStatus.Confirmed);
            Assert.NotNull(confirmationHistory);
            Assert.Equal(OrderStatus.Placed, confirmationHistory.FromStatus);

            // Paid payment record
            var paidPayment = dbOrder.Payments.FirstOrDefault(p => p.PaymentStatus == PaymentStatus.Paid);
            Assert.NotNull(paidPayment);
            Assert.Equal("Simulated", paidPayment.PaymentProvider);
            Assert.False(paidPayment.IsLateSuccess);
            Assert.False(paidPayment.RequiresRefund);
            Assert.Null(paidPayment.RefundReason);
        }

        // 5. Query payment history
        var historyResponse = await client.GetAsync($"/api/orders/{order.Id}/payments");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<PaymentHistoryResponse>>();
        Assert.NotNull(history);
        Assert.Contains(history, p => p.PaymentStatus == "Paid" && p.Id == initResult.PaymentId);
    }

    [Fact]
    public async Task CustomerCannotControlSimulationOutcome_ViaRequestBody()
    {
        // Customer attempts to pass simulation control in request body JSON
        var (token, customerId) = await RegisterCustomerAsync("customer_no_control");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Body includes unauthorized "simulationAction": "Failed"
        var jsonContent = new StringContent(
            "{\"paymentMethod\": 1, \"idempotencyKey\": \"UNAUTH-SIM-KEY\", \"simulationAction\": \"Failed\"}",
            Encoding.UTF8,
            "application/json");

        var initRes = await client.PostAsync($"/api/orders/{order.Id}/payments", jsonContent);
        Assert.Equal(HttpStatusCode.Created, initRes.StatusCode);
        var p = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p);

        // Verify payment: backend provider default (Success) applies, customer simulationAction was ignored!
        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{p.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p.PaymentReference
        });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var verify = await verifyRes.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(verify);
        Assert.Equal("Paid", verify.Status);
        Assert.Equal("Confirmed", verify.OrderStatus);
    }

    [Fact]
    public async Task PaymentVerification_AfterApplicationRestart_ShouldRecoverAndConfirmOrder()
    {
        // 1. Create order and initiate payment
        var (token, customerId) = await RegisterCustomerAsync("restart_recovery");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 2800m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.CreditCard
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.CreditCard,
            IdempotencyKey = "RESTART-KEY-1"
        });
        var initResult = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initResult);

        // 2. Simulate application restart: wipe in-memory sessions cache completely
        SimulatedPaymentProvider.ResetSessionsForTesting();

        // 3. Verify payment: should recover session from self-verifying cryptographic reference
        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{initResult.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initResult.PaymentReference
        });

        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var verify = await verifyRes.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(verify);
        Assert.Equal("Paid", verify.Status);
        Assert.Equal("Confirmed", verify.OrderStatus);
    }

    [Fact]
    public async Task VerifyPayment_WhenLateSuccessAfterOrderCancelled_ShouldRecordPaidAndFlagRefundWithoutReactivatingOrder()
    {
        var (token, customerId) = await RegisterCustomerAsync("late_success_cancelled");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3200m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "LATE-CANCEL-KEY"
        });
        var p = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p);

        // Simulate order cancellation in database while payment was in processing
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            dbOrder.OrderStatus = OrderStatus.Cancelled;
            dbOrder.CancelledDateUtc = DateTime.UtcNow;
            dbOrder.CancellationReason = "Customer cancelled before payment completed.";
            await db.SaveChangesAsync();
        }

        // Verify payment outcome
        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{p.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p.PaymentReference
        });

        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var v = await verifyRes.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(v);
        Assert.Equal("Paid", v.Status);
        Assert.Equal("Cancelled", v.OrderStatus); // Order must remain Cancelled!
        Assert.True(v.IsLateSuccess);
        Assert.True(v.RequiresRefund);
        Assert.Null(v.FailureReason);
        Assert.Contains("LATE_SUCCESS_AFTER_CANCELLATION", v.RefundReason);

        // Verify database state: IsLateSuccess = 1, RequiresRefund = 1, OrderStatus remains Cancelled
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbPayment = await db.Payments.FirstAsync(pay => pay.Id == p.PaymentId);
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);

            Assert.Equal(PaymentStatus.Paid, dbPayment.PaymentStatus);
            Assert.True(dbPayment.IsLateSuccess);
            Assert.True(dbPayment.RequiresRefund);
            Assert.Null(dbPayment.FailureReason);
            Assert.Equal(OrderStatus.Cancelled, dbOrder.OrderStatus);
        }
    }

    [Fact]
    public async Task VerifyPayment_WhenLateSuccessAfterPaymentWindowExpired_ShouldRecordPaidAndFlagRefundWithoutConfirmingOrder()
    {
        var (token, customerId) = await RegisterCustomerAsync("late_success_expired");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3200m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "LATE-EXPIRY-KEY"
        });
        var p = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p);

        // Simulate 30-minute window expiration by updating OrderDateUtc to 35 minutes ago
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
            dbOrder.OrderDateUtc = DateTime.UtcNow.AddMinutes(-35);
            await db.SaveChangesAsync();
        }

        // Verify payment outcome
        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{p.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p.PaymentReference
        });

        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var v = await verifyRes.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(v);
        Assert.Equal("Paid", v.Status);
        Assert.Equal("Placed", v.OrderStatus); // Order must NOT be Confirmed!
        Assert.True(v.IsLateSuccess);
        Assert.True(v.RequiresRefund);
        Assert.Null(v.FailureReason);
        Assert.Contains("LATE_SUCCESS_AFTER_EXPIRY", v.RefundReason);
    }

    [Fact]
    public async Task InitiatePayment_WhenOrderBelongsToAnotherCustomer_ShouldReturnForbiddenOrNotFound()
    {
        // Customer 1 creates order
        var (token1, cust1) = await RegisterCustomerAsync("cust1_payment");
        using var client1 = CreateAuthenticatedClient(token1);
        var address = await SeedAddressAsync(cust1);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await client1.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client1.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Customer 2 attempts to initiate payment for Customer 1's order
        var (token2, _) = await RegisterCustomerAsync("cust2_payment");
        using var client2 = CreateAuthenticatedClient(token2);

        var crossRes = await client2.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI
        });

        Assert.True(crossRes.StatusCode == HttpStatusCode.Unauthorized || crossRes.StatusCode == HttpStatusCode.Forbidden || crossRes.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiatePayment_WhenOrderIsCOD_ShouldReturnBadRequest()
    {
        var (token, customerId) = await RegisterCustomerAsync("cod_payment");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.CreditCard
        });

        Assert.Equal(HttpStatusCode.BadRequest, initRes.StatusCode);
    }

    [Fact]
    public async Task VerifyPayment_WhenDefinitiveFailureOccurs_ShouldRecordFailedAndAllowRetry()
    {
        var (token, customerId) = await RegisterCustomerAsync("failed_retry_flow");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.CreditCard
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 1. First attempt simulates failure via development header
        client.DefaultRequestHeaders.Add("X-Simulation-Action", "Failed");
        var init1 = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.CreditCard,
            IdempotencyKey = "ATTEMPT-1"
        });
        var p1 = await init1.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p1);

        // Verify attempt 1 -> returns Failed
        var verify1 = await client.PostAsJsonAsync($"/api/payments/{p1.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p1.PaymentReference
        });
        Assert.Equal(HttpStatusCode.OK, verify1.StatusCode);
        var v1 = await verify1.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(v1);
        Assert.Equal("Failed", v1.Status);
        Assert.Equal("Placed", v1.OrderStatus); // Order remains Placed!

        // 2. Retry with a new attempt and new idempotency key -> normal Success
        client.DefaultRequestHeaders.Remove("X-Simulation-Action");
        var init2 = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.CreditCard,
            IdempotencyKey = "ATTEMPT-2"
        });
        Assert.Equal(HttpStatusCode.Created, init2.StatusCode);
        var p2 = await init2.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p2);
        Assert.NotEqual(p1.PaymentId, p2.PaymentId);

        // Verify attempt 2 -> returns Paid, order moves to Confirmed
        var verify2 = await client.PostAsJsonAsync($"/api/payments/{p2.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p2.PaymentReference
        });
        Assert.Equal(HttpStatusCode.OK, verify2.StatusCode);
        var v2 = await verify2.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(v2);
        Assert.Equal("Paid", v2.Status);
        Assert.Equal("Confirmed", v2.OrderStatus);

        // 3. Payment history should list both failed and paid attempts
        var historyRes = await client.GetAsync($"/api/orders/{order.Id}/payments");
        var history = await historyRes.Content.ReadFromJsonAsync<List<PaymentHistoryResponse>>();
        Assert.NotNull(history);
        Assert.Equal(3, history.Count); // Initial Pending from checkout + Failed attempt 1 + Paid attempt 2
    }

    [Fact]
    public async Task ConcurrentInitiations_OnSameOrder_OnlyOneCanSucceed()
    {
        var (token, customerId) = await RegisterCustomerAsync("concurrent_init");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 4000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.NetBanking
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Send two concurrent initiation requests
        var task1 = client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.NetBanking,
            IdempotencyKey = "CONCUR-1"
        });
        var task2 = client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.NetBanking,
            IdempotencyKey = "CONCUR-2"
        });

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        // Exactly one should succeed with 201 Created; the other should fail with 409 Conflict
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        // In database, exactly one Processing record exists
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processingCount = await db.Payments.CountAsync(p => p.OrderId == order.Id && p.PaymentStatus == PaymentStatus.Processing);
        Assert.Equal(1, processingCount);
    }

    [Fact]
    public async Task ConcurrentVerifications_OnSamePaymentAttempt_HandlesSafelyAndExactlyOnceTransition()
    {
        var (token, customerId) = await RegisterCustomerAsync("concurrent_verify");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 4500m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Initiate payment
        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "CONCUR-VERIFY-INIT"
        });
        var p = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(p);

        // Send two concurrent verification requests for the same payment
        var task1 = client.PostAsJsonAsync($"/api/payments/{p.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p.PaymentReference
        });
        var task2 = client.PostAsJsonAsync($"/api/payments/{p.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = p.PaymentReference
        });

        var responses = await Task.WhenAll(task1, task2);

        // Both should complete safely (either 200 OK or 409 Conflict), but database must remain consistent
        foreach (var r in responses)
        {
            Assert.True(r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var dbOrder = await db.Orders
            .Include(o => o.Payments)
            .Include(o => o.StatusHistories)
            .FirstAsync(o => o.Id == order.Id);

        Assert.Equal(OrderStatus.Confirmed, dbOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, dbOrder.PaymentStatus);

        // Filtered unique index ensures exactly ONE Paid payment
        var paidCount = dbOrder.Payments.Count(pay => pay.PaymentStatus == PaymentStatus.Paid);
        Assert.Equal(1, paidCount);

        // OrderStatusHistory should have exactly one Placed -> Confirmed transition
        var confirmedTransitions = dbOrder.StatusHistories.Count(h => h.ToStatus == OrderStatus.Confirmed);
        Assert.Equal(1, confirmedTransitions);
    }

    [Fact]
    public async Task Employee_CanViewAnyCustomerOrderPaymentHistory()
    {
        // 1. Customer creates order and initiates payment
        var (custToken, customerId) = await RegisterCustomerAsync("employee_view_pay");
        using var custClient = CreateAuthenticatedClient(custToken);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3500m);

        await custClient.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await custClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 2. Employee queries payment history
        var empToken = GenerateTokenForRole(UserRole.Employee, 9999, "employee_pay@sareestore.com");
        using var empClient = CreateAuthenticatedClient(empToken);

        var res = await empClient.GetAsync($"/api/orders/{order.Id}/payments");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var history = await res.Content.ReadFromJsonAsync<List<PaymentHistoryResponse>>();
        Assert.NotNull(history);
        Assert.NotEmpty(history);
    }

    // --- Helper Methods ---

    private async Task<(string Token, int UserId)> RegisterCustomerAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{prefix}_{suffix}@test.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = $"{prefix} User",
            Email = email,
            Password = "Password123#",
            PhoneNumber = "+919876543210"
        });

        registerResponse.EnsureSuccessStatusCode();
        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);

        return (authResult.AccessToken, authResult.User.Id);
    }

    private async Task<Address> SeedAddressAsync(int customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var address = new Address
        {
            UserId = customerId,
            RecipientName = "Payment Test Recipient",
            PhoneNumber = "+919876543210",
            AddressLine1 = "123 Silk Board",
            City = "Bengaluru",
            State = "Karnataka",
            PostalCode = "560068",
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
            Name = $"Pay Cat {suffix}",
            Slug = $"pay-cat-{suffix}",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            SKU = $"PAY-SKU-{suffix}",
            Title = $"Payment Test Saree {suffix}",
            Slug = $"pay-saree-{suffix}",
            Description = "Test saree for payment tests",
            Price = price,
            DiscountPrice = discountPrice,
            Fabric = "Silk",
            WeaveType = "Handloom",
            ZariType = "Pure Zari",
            PrimaryColor = "Blue",
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
