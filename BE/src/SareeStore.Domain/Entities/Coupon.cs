using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxDiscountCap { get; set; }
    public int? UsageLimit { get; set; }
    public int TimesUsed { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
