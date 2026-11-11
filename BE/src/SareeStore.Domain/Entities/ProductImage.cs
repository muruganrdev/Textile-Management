using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }

    // Navigation property
    public Product Product { get; set; } = null!;
}
