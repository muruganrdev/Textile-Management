using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SareeStore.Application.Common.Exceptions;
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
public class OrderCancellationApiTests : IClassFixture<SareeStoreApiFactory>
{
    private readonly SareeStoreApiFactory _factory;
    private readonly HttpClient _client;

    public OrderCancellationApiTests(SareeStoreApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SimulatedPaymentProvider.ResetSessionsForTesting();
        SimulatedRefundProvider.ResetSessionsForTesting();
    }

    [Fact]
    public async Task Customer_CanCancelOwnPlacedOrder_RestoresStockAndUpdatesStatus()
    {
        // 1. Setup customer and stock
        var (token, customerId) = await RegisterCustomerAsync("cancel_placed");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 4000m);

        // 2. Add to cart & checkout
        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Verify stock deducted
        var stockAfterCheckout = await GetProductStockAsync(product.Id);
        Assert.Equal(4, stockAfterCheckout);

        // 3. Cancel order
        var cancelRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Changed my mind"
        });

        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelResult = await cancelRes.Content.ReadFromJsonAsync<CancelOrderResponse>();
        Assert.NotNull(cancelResult);
        Assert.Equal(order.Id, cancelResult.OrderId);
        Assert.Equal("Cancelled", cancelResult.OrderStatus);
        Assert.True(cancelResult.IsStockRestored);
        Assert.NotNull(cancelResult.StockRestoredAtUtc);
        Assert.False(cancelResult.RefundInitiated);

        // 4. Verify database state
        var restoredStock = await GetProductStockAsync(product.Id);
        Assert.Equal(5, restoredStock);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbOrder = await db.Orders.Include(o => o.StatusHistories).FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(dbOrder);
        Assert.Equal(OrderStatus.Cancelled, dbOrder.OrderStatus);
        Assert.True(dbOrder.IsStockRestored);
        Assert.Contains(dbOrder.StatusHistories, h => h.ToStatus == OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Customer_CanCancelOwnConfirmedPaidOrder_RestoresStockAndInitiatesAutomatedRefund()
    {
        // 1. Setup customer, product, and checkout
        var (token, customerId) = await RegisterCustomerAsync("cancel_paid");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 6000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 2. Initiate & Verify payment
        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // 3. Cancel order
        var cancelRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Product no longer needed"
        });

        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelResult = await cancelRes.Content.ReadFromJsonAsync<CancelOrderResponse>();
        Assert.NotNull(cancelResult);
        Assert.Equal("Cancelled", cancelResult.OrderStatus);
        Assert.True(cancelResult.IsStockRestored);
        Assert.True(cancelResult.RefundInitiated);
        Assert.Equal("Success", cancelResult.RefundStatus);
        Assert.Equal(6000m, cancelResult.RefundAmount);
        Assert.NotNull(cancelResult.RefundReference);

        // 4. Verify Database state: stock restored and payment/refund updated
        var restoredStock = await GetProductStockAsync(product.Id);
        Assert.Equal(5, restoredStock);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbOrder = await db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == order.Id);
        Assert.NotNull(dbOrder);
        Assert.Equal(OrderStatus.Cancelled, dbOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Refunded, dbOrder.PaymentStatus);

        var dbRefund = await db.Refunds.FirstOrDefaultAsync(r => r.OrderId == order.Id);
        Assert.NotNull(dbRefund);
        Assert.Equal(RefundStatus.Success, dbRefund.Status);
        Assert.Equal(6000m, dbRefund.Amount);
    }

    [Fact]
    public async Task Customer_CannotCancelAnotherCustomerOrder_ReturnsForbidden()
    {
        // 1. Customer A creates order
        var (tokenA, customerIdA) = await RegisterCustomerAsync("custA");
        using var clientA = CreateAuthenticatedClient(tokenA);
        var addressA = await SeedAddressAsync(customerIdA);
        var product = await SeedProductAsync(stock: 5, price: 3000m);

        await clientA.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await clientA.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = addressA.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // 2. Customer B attempts to cancel Customer A's order
        var (tokenB, _) = await RegisterCustomerAsync("custB");
        using var clientB = CreateAuthenticatedClient(tokenB);

        var cancelRes = await clientB.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Malicious cancellation"
        });

        Assert.Equal(HttpStatusCode.Forbidden, cancelRes.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotCancelProcessingOrDeliveredOrder()
    {
        var (token, customerId) = await RegisterCustomerAsync("cancel_status");
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

        // Manually advance order status in DB to Processing
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.NotNull(dbOrder);
            dbOrder.OrderStatus = OrderStatus.Processing;
            await db.SaveChangesAsync();
        }

        // Attempt cancellation
        var cancelRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Cancel after processing"
        });

        Assert.Equal(HttpStatusCode.BadRequest, cancelRes.StatusCode);
    }

    [Fact]
    public async Task DuplicateCancellation_DoesNotRestoreStockTwice()
    {
        var (token, customerId) = await RegisterCustomerAsync("dup_cancel");
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

        // First cancellation succeeds
        var firstCancel = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "First cancellation"
        });
        Assert.Equal(HttpStatusCode.OK, firstCancel.StatusCode);

        var stockAfterFirstCancel = await GetProductStockAsync(product.Id);
        Assert.Equal(5, stockAfterFirstCancel);

        // Second cancellation returns Conflict
        var secondCancel = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Second cancellation"
        });
        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);

        // Stock must still be exactly 5, NOT 6!
        var stockAfterSecondCancel = await GetProductStockAsync(product.Id);
        Assert.Equal(5, stockAfterSecondCancel);
    }

    [Fact]
    public async Task LatePaymentVerification_AfterCancellation_PreservesCancelledStatusAndFlagsRequiresRefund()
    {
        var (token, customerId) = await RegisterCustomerAsync("late_pay_cancel");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 5500m);

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
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        // Cancel order BEFORE payment verification
        var cancelRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Decided to cancel before paying"
        });
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var stockAfterCancel = await GetProductStockAsync(product.Id);
        Assert.Equal(5, stockAfterCancel);

        // Now late payment verification succeeds
        var verifyRes = await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var verifyData = await verifyRes.Content.ReadFromJsonAsync<PaymentVerifyResponse>();
        Assert.NotNull(verifyData);
        Assert.Equal("Paid", verifyData.Status);
        Assert.Equal("Cancelled", verifyData.OrderStatus);
        Assert.True(verifyData.IsLateSuccess);
        Assert.True(verifyData.RequiresRefund);

        // Stock must NOT be restored a second time
        var stockAfterLateVerify = await GetProductStockAsync(product.Id);
        Assert.Equal(5, stockAfterLateVerify);
    }

    [Fact]
    public async Task Customer_CanViewOrderRefunds()
    {
        var (token, customerId) = await RegisterCustomerAsync("view_refunds");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 7000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Pay and cancel
        var initRes = await client.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
        {
            Reason = "Cancelled for refund query test"
        });

        // Query refunds
        var refundsRes = await client.GetAsync($"/api/orders/{order.Id}/refunds");
        Assert.Equal(HttpStatusCode.OK, refundsRes.StatusCode);

        var refunds = await refundsRes.Content.ReadFromJsonAsync<List<RefundResponse>>();
        Assert.NotNull(refunds);
        Assert.NotEmpty(refunds);
        Assert.Equal("Success", refunds[0].Status);
        Assert.Equal(7000m, refunds[0].Amount);
    }

    [Fact]
    public async Task SimulatedApplicationInterruption_AfterCancellationCommit_DoesNotLoseDurableRefundIntent_AndRecoverySucceeds()
    {
        // 1. Setup customer and paid online order
        var (token, customerId) = await RegisterCustomerAsync("durable_refund_intent");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 10, price: 4500m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 2 });
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
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        // 2. Simulate interruption: Call OrderRepository.CancelOrderWithStockRestorationAsync directly,
        // simulating the database transaction committing, followed immediately by application interruption
        // before provider initiation happens
        using (var scope = _factory.Services.CreateScope())
        {
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var cancelledOrder = await orderRepo.CancelOrderWithStockRestorationAsync(
                order.Id, customerId, null, "Interrupted before provider call");

            Assert.Equal(OrderStatus.Cancelled, cancelledOrder.OrderStatus);
            Assert.True(cancelledOrder.IsStockRestored);
        }

        // 3. Verify Database state: Refund is stored durable in Pending status! Stock is restored!
        int refundId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbRefund = await db.Refunds.FirstOrDefaultAsync(r => r.OrderId == order.Id);
            Assert.NotNull(dbRefund);
            Assert.Equal(RefundStatus.Pending, dbRefund.Status);
            Assert.Equal(9000m, dbRefund.Amount);
            Assert.NotNull(dbRefund.IdempotencyKey);
            refundId = dbRefund.Id;
        }

        var stockAfterCommit = await GetProductStockAsync(product.Id);
        Assert.Equal(10, stockAfterCommit);

        // 4. Recovery: Reconcile endpoint resumes the existing Pending refund without creating duplicates
        var reconcileRes = await client.PostAsync($"/api/orders/{order.Id}/refunds/{refundId}/reconcile", null);
        Assert.Equal(HttpStatusCode.OK, reconcileRes.StatusCode);

        var reconcileResult = await reconcileRes.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(reconcileResult);
        Assert.Equal("Success", reconcileResult.Status);
        Assert.Equal(9000m, reconcileResult.Amount);
        Assert.NotNull(reconcileResult.RefundReference);

        // 5. Verify exactly one refund record exists in DB and payment/order are Refunded
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refunds = await db.Refunds.Where(r => r.OrderId == order.Id).ToListAsync();
            Assert.Single(refunds);
            Assert.Equal(RefundStatus.Success, refunds[0].Status);

            var dbOrder = await db.Orders.FindAsync(order.Id);
            Assert.NotNull(dbOrder);
            Assert.Equal(PaymentStatus.Refunded, dbOrder.PaymentStatus);
        }
    }

    [Fact]
    public async Task ConcurrentCancellationRequests_DoNotRestoreStockTwice()
    {
        var (token, customerId) = await RegisterCustomerAsync("concurrent_cancel");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 10, price: 3000m);

        await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 2 });
        var checkoutRes = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        // Stock after checkout = 8
        var stockAfterCheckout = await GetProductStockAsync(product.Id);
        Assert.Equal(8, stockAfterCheckout);

        // Launch 4 concurrent cancellation requests simultaneously
        var tasks = Enumerable.Range(1, 4).Select(i =>
            client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest
            {
                Reason = $"Concurrent cancel attempt {i}"
            })).ToList();

        var responses = await Task.WhenAll(tasks);

        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(3, conflictCount);

        // Stock must be restored exactly once (8 + 2 = 10, NOT 16!)
        var finalStock = await GetProductStockAsync(product.Id);
        Assert.Equal(10, finalStock);
    }

    [Fact]
    public async Task RefundTimeout_RemainsRecoverable_AndCanBeReconciled()
    {
        var (token, customerId) = await RegisterCustomerAsync("refund_timeout");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 3500m);

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
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        // 1. Cancel order with RefundTimeout simulation header
        using var timeoutRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/cancel")
        {
            Content = JsonContent.Create(new CancelOrderRequest { Reason = "Timeout test" })
        };
        timeoutRequest.Headers.Add("X-Simulation-Action", "RefundTimeout");

        var cancelRes = await client.SendAsync(timeoutRequest);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var cancelData = await cancelRes.Content.ReadFromJsonAsync<CancelOrderResponse>();
        Assert.NotNull(cancelData);
        Assert.Equal("Processing", cancelData.RefundStatus);

        // In DB, status is Processing and failure reason notes timeout
        int refundId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbRefund = await db.Refunds.FirstOrDefaultAsync(r => r.OrderId == order.Id);
            Assert.NotNull(dbRefund);
            Assert.Equal(RefundStatus.Processing, dbRefund.Status);
            Assert.Contains("REFUND_TIMEOUT", dbRefund.FailureReason);
            refundId = dbRefund.Id;
        }

        // 2. Reconcile refund without timeout header -> transitions to Success
        var reconcileRes = await client.PostAsync($"/api/orders/{order.Id}/refunds/{refundId}/reconcile", null);
        Assert.Equal(HttpStatusCode.OK, reconcileRes.StatusCode);

        var reconcileData = await reconcileRes.Content.ReadFromJsonAsync<RefundResponse>();
        Assert.NotNull(reconcileData);
        Assert.Equal("Success", reconcileData.Status);
    }

    [Fact]
    public async Task RefundProviderRejection_RecordedAsFailed()
    {
        var (token, customerId) = await RegisterCustomerAsync("refund_decline");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 4200m);

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
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        // Cancel with RefundDeclined simulation header
        using var declineRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/cancel")
        {
            Content = JsonContent.Create(new CancelOrderRequest { Reason = "Decline test" })
        };
        declineRequest.Headers.Add("X-Simulation-Action", "RefundDeclined");

        var cancelRes = await client.SendAsync(declineRequest);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        var cancelData = await cancelRes.Content.ReadFromJsonAsync<CancelOrderResponse>();
        Assert.NotNull(cancelData);
        Assert.Equal("Failed", cancelData.RefundStatus);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbRefund = await db.Refunds.FirstOrDefaultAsync(r => r.OrderId == order.Id);
        Assert.NotNull(dbRefund);
        Assert.Equal(RefundStatus.Failed, dbRefund.Status);
        Assert.Contains("declined", dbRefund.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinancialIntegrity_RefundAmountCannotExceedCapturedPaymentAmount()
    {
        var (token, customerId) = await RegisterCustomerAsync("financial_integrity");
        using var client = CreateAuthenticatedClient(token);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 5, price: 5000m);

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
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await client.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        // Cancel order -> processes 5000 refund
        await client.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest { Reason = "Full refund" });

        // Now attempt to process an over-refunding second refund directly via repository
        using var scope = _factory.Services.CreateScope();
        var refundRepo = scope.ServiceProvider.GetRequiredService<IRefundRepository>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var extraRefund = new Refund
        {
            OrderId = order.Id,
            PaymentId = initData.PaymentId,
            Amount = 1000m,
            Currency = "INR",
            Status = RefundStatus.Processing,
            Reason = "Attempted over-refund",
            IdempotencyKey = "EXTRA-REFUND-1",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Refunds.Add(extraRefund);
        await db.SaveChangesAsync();

        // Financial integrity check throws ConflictException
        await Assert.ThrowsAsync<ConflictException>(() =>
            refundRepo.ProcessSuccessfulRefundAsync(extraRefund.Id, "SIM-EXTRA-REF"));
    }

    [Fact]
    public async Task SuperAdminCancellation_FollowsExplicitStatusRules()
    {
        var (superAdminToken, adminUserId) = await SeedSuperAdminUserAsync("superadmin_rules");
        using var adminClient = CreateAuthenticatedClient(superAdminToken);

        var (custToken, customerId) = await RegisterCustomerAsync("admin_cancel_cust");
        using var custClient = CreateAuthenticatedClient(custToken);
        var address = await SeedAddressAsync(customerId);
        var product = await SeedProductAsync(stock: 10, price: 3000m);

        // 1. SuperAdmin can cancel Processing order
        await custClient.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var order1Res = await custClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order1 = await order1Res.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FindAsync(order1.Id);
            Assert.NotNull(dbOrder);
            dbOrder.OrderStatus = OrderStatus.Processing;
            await db.SaveChangesAsync();
        }

        var adminCancelRes = await adminClient.PostAsJsonAsync($"/api/orders/{order1.Id}/admin-cancel", new CancelOrderRequest
        {
            Reason = "Administrative cancellation of processing order"
        });
        Assert.Equal(HttpStatusCode.OK, adminCancelRes.StatusCode);

        // 2. SuperAdmin CANNOT cancel Dispatched or Delivered order
        await custClient.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var order2Res = await custClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = address.Id,
            PaymentMethod = PaymentMethod.COD
        });
        var order2 = await order2Res.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await db.Orders.FindAsync(order2.Id);
            Assert.NotNull(dbOrder);
            dbOrder.OrderStatus = OrderStatus.Delivered;
            await db.SaveChangesAsync();
        }

        var adminCancelDelivered = await adminClient.PostAsJsonAsync($"/api/orders/{order2.Id}/admin-cancel", new CancelOrderRequest
        {
            Reason = "Attempt to cancel delivered order"
        });
        Assert.Equal(HttpStatusCode.BadRequest, adminCancelDelivered.StatusCode);
    }

    [Fact]
    public async Task Employee_CannotAccessAdministrativeCancellation_Returns403Forbidden()
    {
        var employeeToken = GenerateTokenForRole(UserRole.Employee, 801, "employee_cancel@sareestore.com");
        using var employeeClient = CreateAuthenticatedClient(employeeToken);

        var cancelRes = await employeeClient.PostAsJsonAsync("/api/orders/999/admin-cancel", new CancelOrderRequest
        {
            Reason = "Unauthorized employee cancellation"
        });

        Assert.Equal(HttpStatusCode.Forbidden, cancelRes.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotAccessAnotherCustomerRefundHistory_Returns403Forbidden()
    {
        // 1. Customer A creates paid order and cancels it
        var (tokenA, customerIdA) = await RegisterCustomerAsync("custA_refunds");
        using var clientA = CreateAuthenticatedClient(tokenA);
        var addressA = await SeedAddressAsync(customerIdA);
        var product = await SeedProductAsync(stock: 5, price: 4000m);

        await clientA.PostAsJsonAsync("/api/cart/items", new AddToCartRequest { ProductId = product.Id, Quantity = 1 });
        var checkoutRes = await clientA.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            ShippingAddressId = addressA.Id,
            PaymentMethod = PaymentMethod.UPI
        });
        var order = await checkoutRes.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        var initRes = await clientA.PostAsJsonAsync($"/api/orders/{order.Id}/payments", new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = $"PAY-INIT-{Guid.NewGuid():N}"
        });
        var initData = await initRes.Content.ReadFromJsonAsync<PaymentInitiateResponse>();
        Assert.NotNull(initData);

        await clientA.PostAsJsonAsync($"/api/payments/{initData.PaymentId}/verify", new VerifyPaymentRequest
        {
            PaymentReference = initData.PaymentReference
        });

        await clientA.PostAsJsonAsync($"/api/orders/{order.Id}/cancel", new CancelOrderRequest { Reason = "Cancelled" });

        // 2. Customer B attempts to view Customer A's refunds
        var (tokenB, _) = await RegisterCustomerAsync("custB_snooper");
        using var clientB = CreateAuthenticatedClient(tokenB);

        var crossRefundsRes = await clientB.GetAsync($"/api/orders/{order.Id}/refunds");
        Assert.Equal(HttpStatusCode.Forbidden, crossRefundsRes.StatusCode);
    }

    // --- Helpers ---

    private async Task<(string AccessToken, int UserId)> RegisterCustomerAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{prefix}_{suffix}@test.com";

        var res = await _client.PostAsJsonAsync("/api/auth/register", new RegisterCustomerRequest
        {
            FullName = $"{prefix} User",
            Email = email,
            Password = "Password123#",
            PhoneNumber = "+919876543210"
        });

        res.EnsureSuccessStatusCode();
        var authResult = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return (authResult!.AccessToken, authResult.User.Id);
    }

    private async Task<Address> SeedAddressAsync(int customerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var address = new Address
        {
            UserId = customerId,
            RecipientName = "Cancellation Test Recipient",
            PhoneNumber = "+919876543210",
            AddressLine1 = "77 Palace Road",
            City = "Bengaluru",
            State = "Karnataka",
            PostalCode = "560001",
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
            Name = $"Cancel Cat {suffix}",
            Slug = $"cancel-cat-{suffix}",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.Id,
            Title = $"Cancel Saree {suffix}",
            Slug = $"cancel-saree-{suffix}",
            SKU = $"CAN-{suffix.ToUpperInvariant()}",
            Price = price,
            DiscountPrice = discountPrice,
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

    private async Task<int> GetProductStockAsync(int productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventory = await db.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
        return inventory?.StockQuantity ?? 0;
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

    private async Task<(string Token, int UserId)> SeedSuperAdminUserAsync(string emailPrefix)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var admin = new User
        {
            FullName = "Super Admin User",
            Email = $"{emailPrefix}_{suffix}@sareestore.com",
            PasswordHash = "AQAAAAIAAYagAAAAEOc7...",
            Role = UserRole.SuperAdmin,
            IsActive = true
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var token = tokenGenerator.GenerateToken(admin);
        return (token, admin.Id);
    }
}
