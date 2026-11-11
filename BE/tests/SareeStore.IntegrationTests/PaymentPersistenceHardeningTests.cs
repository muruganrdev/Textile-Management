using Microsoft.EntityFrameworkCore;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using Xunit;

namespace SareeStore.IntegrationTests;

[Collection("DatabaseTests")]
public class PaymentPersistenceHardeningTests
{
    private async Task<(User user, Order order)> SeedUserAndOrderAsync(DbContext context)
    {
        var user = new User
        {
            FullName = "Persistence Test Customer",
            Email = $"persist-test-{Guid.NewGuid():N}@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Customer,
            IsActive = true
        };
        context.Add(user);
        await context.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = $"ORD-PER-{Guid.NewGuid():N}",
            CustomerId = user.Id,
            SubTotal = 5000m,
            TotalAmount = 5000m,
            OrderStatus = OrderStatus.Placed,
            PaymentStatus = PaymentStatus.Pending,
            ShippingRecipientName = "Persistence Customer",
            ShippingPhoneNumber = "9876543210",
            ShippingAddressLine1 = "456 Temple Road",
            ShippingCity = "Kanchipuram",
            ShippingState = "Tamil Nadu",
            ShippingPostalCode = "631501",
            ShippingCountry = "India"
        };
        context.Add(order);
        await context.SaveChangesAsync();

        return (user, order);
    }

    [Fact]
    public async Task Order_CheckConstraint_StockRestoredWithoutTimestamp_ShouldThrowDbUpdateException()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order) = await SeedUserAndOrderAsync(context);

        // Violate constraint: IsStockRestored = true, but StockRestoredAtUtc = null
        order.IsStockRestored = true;
        order.StockRestoredAtUtc = null;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_Orders_StockRestored_Timestamp", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task Order_CheckConstraint_StockRestoredWithTimestamp_ShouldSucceed()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order) = await SeedUserAndOrderAsync(context);

        // Satisfy constraint: IsStockRestored = true AND StockRestoredAtUtc is set
        order.IsStockRestored = true;
        order.StockRestoredAtUtc = DateTime.UtcNow;
        order.CancellationReason = "Customer requested cancellation before processing";

        var saved = await context.SaveChangesAsync();
        Assert.True(saved > 0);
    }

    [Fact]
    public async Task Payment_FilteredUniqueIndex_OrderId_Paid_ShouldPreventDuplicatePaidRecordsForSameOrder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order) = await SeedUserAndOrderAsync(context);

        // First successful payment
        var payment1 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 5000m,
            TransactionId = $"TXN-PAID-1-{Guid.NewGuid():N}",
            PaidAtUtc = DateTime.UtcNow,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(payment1);
        await context.SaveChangesAsync();

        // Second successful payment for the same order must violate IX_Payments_OrderId_Paid
        var payment2 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.CreditCard,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 5000m,
            TransactionId = $"TXN-PAID-2-{Guid.NewGuid():N}",
            PaidAtUtc = DateTime.UtcNow,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(payment2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("IX_Payments_OrderId_Paid", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task Payment_MultipleFailedAttemptsWithOnePaid_ShouldSucceed()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order) = await SeedUserAndOrderAsync(context);

        // Failed attempt 1
        var failedPayment1 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Failed,
            Amount = 5000m,
            FailureReason = "Bank network timeout",
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(failedPayment1);

        // Failed attempt 2
        var failedPayment2 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.CreditCard,
            PaymentStatus = PaymentStatus.Failed,
            Amount = 5000m,
            FailureReason = "Card declined",
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(failedPayment2);

        // Final successful attempt
        var successfulPayment = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Paid,
            Amount = 5000m,
            TransactionId = $"TXN-SUCCESS-{Guid.NewGuid():N}",
            PaidAtUtc = DateTime.UtcNow,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(successfulPayment);

        var saved = await context.SaveChangesAsync();
        Assert.True(saved > 0);
    }

    [Fact]
    public async Task Payment_FilteredUniqueIndex_OrderId_Processing_ShouldPreventDuplicateActiveProcessingAttempts()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order) = await SeedUserAndOrderAsync(context);

        // First active processing payment
        var processing1 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            Amount = 5000m,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(processing1);
        await context.SaveChangesAsync();

        // Second concurrent processing payment for the same order must violate IX_Payments_OrderId_Processing
        var processing2 = new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            Amount = 5000m,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(processing2);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("IX_Payments_OrderId_Processing", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task Payment_FilteredUniqueIndex_OrderId_IdempotencyKey_ShouldEnforcePerOrderUniqueness()
    {
        await using var context = TestDbContextFactory.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var (_, order1) = await SeedUserAndOrderAsync(context);
        var (_, order2) = await SeedUserAndOrderAsync(context);

        var sharedKey = $"IDEMP-{Guid.NewGuid():N}";

        // Payment on Order 1 with sharedKey
        var p1 = new Payment
        {
            OrderId = order1.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            Amount = 5000m,
            IdempotencyKey = sharedKey,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(p1);

        // Payment on Order 2 with SAME sharedKey should SUCCEED because idempotency key is scoped to OrderId
        var p2 = new Payment
        {
            OrderId = order2.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            Amount = 5000m,
            IdempotencyKey = sharedKey,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(p2);

        await context.SaveChangesAsync();

        // Duplicate payment on Order 1 with same sharedKey should FAIL
        var p1Duplicate = new Payment
        {
            OrderId = order1.Id,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Failed,
            Amount = 5000m,
            IdempotencyKey = sharedKey,
            PaymentProvider = "Simulated"
        };
        context.Payments.Add(p1Duplicate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("IX_Payments_OrderId_IdempotencyKey", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task ExistingRecords_PreservedAndContainDefaultValues()
    {
        await using var context = TestDbContextFactory.CreateContext();

        var existingOrders = await context.Orders.Take(5).ToListAsync();
        Assert.NotEmpty(existingOrders);

        foreach (var order in existingOrders)
        {
            Assert.False(order.IsStockRestored);
            Assert.Null(order.StockRestoredAtUtc);
        }

        var existingPayments = await context.Payments.Take(5).ToListAsync();
        Assert.NotEmpty(existingPayments);

        foreach (var payment in existingPayments)
        {
            Assert.Equal("Simulated", payment.PaymentProvider);
            Assert.Null(payment.IdempotencyKey);
        }
    }
}
