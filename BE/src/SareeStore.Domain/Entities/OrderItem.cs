using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    // Immutable Historical Snapshots
    public string ProductTitleSnapshot { get; set; } = string.Empty;
    public string ProductSkuSnapshot { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
