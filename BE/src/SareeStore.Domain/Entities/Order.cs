using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public int? CouponId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Placed;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // Immutable Shipping Address Snapshots
    public string ShippingRecipientName { get; set; } = string.Empty;
    public string ShippingPhoneNumber { get; set; } = string.Empty;
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string? ShippingAddressLine2 { get; set; }
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = "India";

    public DateTime OrderDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredDateUtc { get; set; }
    public DateTime? CancelledDateUtc { get; set; }
    public bool IsStockRestored { get; set; } = false;
    public DateTime? StockRestoredAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    // Navigation properties
    public User Customer { get; set; } = null!;
    public Coupon? Coupon { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<OrderStatusHistory> StatusHistories { get; set; } = new List<OrderStatusHistory>();
    public ICollection<OrderReturn> Returns { get; set; } = new List<OrderReturn>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
