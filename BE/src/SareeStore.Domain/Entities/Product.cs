using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Product : BaseEntity
{
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Fabric { get; set; } = string.Empty;
    public string WeaveType { get; set; } = string.Empty;
    public string ZariType { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public decimal SareeLengthMeters { get; set; } = 5.50m;
    public decimal SareeWidthInches { get; set; } = 48.00m;
    public bool BlouseIncluded { get; set; } = true;
    public decimal? BlouseLengthMeters { get; set; } = 0.80m;
    public string? CraftDetails { get; set; }
    public string? Occasion { get; set; }
    public bool Has3DModel { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Category Category { get; set; } = null!;
    public Inventory? Inventory { get; set; }
    public Product3DModel? Product3DModel { get; set; }
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductCollection> ProductCollections { get; set; } = new List<ProductCollection>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
