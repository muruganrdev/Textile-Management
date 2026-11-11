using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Inventory : BaseEntity
{
    public int ProductId { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; } = 2;
    public byte[] RowVersion { get; set; } = [];
    public DateTime? LastRestockedAtUtc { get; set; }

    // Navigation property
    public Product Product { get; set; } = null!;
}
