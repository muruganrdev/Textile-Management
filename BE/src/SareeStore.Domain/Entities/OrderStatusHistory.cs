using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class OrderStatusHistory : BaseEntity
{
    public int OrderId { get; set; }
    public int? ProcessedByUserId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Remarks { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Order Order { get; set; } = null!;
    public User? ProcessedByUser { get; set; }
}
