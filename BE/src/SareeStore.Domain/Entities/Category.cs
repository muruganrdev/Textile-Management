using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
