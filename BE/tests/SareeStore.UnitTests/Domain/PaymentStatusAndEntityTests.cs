using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using Xunit;

namespace SareeStore.UnitTests.Domain;

public class PaymentStatusAndEntityTests
{
    [Fact]
    public void PaymentStatus_NumericValues_MustPreserveExistingAndMapNewStatesCorrectly()
    {
        // Existing mappings must be preserved exactly
        Assert.Equal(1, (int)PaymentStatus.Pending);
        Assert.Equal(2, (int)PaymentStatus.Paid);
        Assert.Equal(3, (int)PaymentStatus.Failed);
        Assert.Equal(4, (int)PaymentStatus.Refunded);

        // New lifecycle states added in Step 5.2
        Assert.Equal(5, (int)PaymentStatus.Processing);
        Assert.Equal(6, (int)PaymentStatus.Cancelled);
    }

    [Fact]
    public void Order_NewStockRestorationProperties_DefaultValuesMustBeSafe()
    {
        var order = new Order();

        Assert.False(order.IsStockRestored);
        Assert.Null(order.StockRestoredAtUtc);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public void Payment_NewProperties_DefaultValuesMustBeSafe()
    {
        var payment = new Payment();

        Assert.Equal("Simulated", payment.PaymentProvider);
        Assert.Null(payment.IdempotencyKey);
        Assert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
    }
}
