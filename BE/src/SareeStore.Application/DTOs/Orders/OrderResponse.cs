namespace SareeStore.Application.DTOs.Orders;

public class OrderResponse
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;

    // Delivery address snapshot
    public string ShippingRecipientName { get; set; } = string.Empty;
    public string ShippingPhoneNumber { get; set; } = string.Empty;
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string? ShippingAddressLine2 { get; set; }
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = "India";

    public DateTime OrderDateUtc { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
}
