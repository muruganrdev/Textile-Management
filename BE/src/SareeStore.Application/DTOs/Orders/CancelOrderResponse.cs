namespace SareeStore.Application.DTOs.Orders;

public class CancelOrderResponse
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public bool IsStockRestored { get; set; }
    public DateTime? StockRestoredAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public bool RefundInitiated { get; set; }
    public string? RefundStatus { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReference { get; set; }
}
