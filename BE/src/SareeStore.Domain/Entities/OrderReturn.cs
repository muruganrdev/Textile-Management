using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class OrderReturn : BaseEntity
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public int? ProcessedByUserId { get; set; }
    public OrderReturnType Type { get; set; } = OrderReturnType.Return;
    public string Reason { get; set; } = string.Empty;
    public OrderReturnStatus Status { get; set; } = OrderReturnStatus.Requested;
    public bool IsStockRestored { get; set; }
    public DateTime? StockRestoredAtUtc { get; set; }
    public decimal RefundAmount { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public User? ProcessedByUser { get; set; }
}
