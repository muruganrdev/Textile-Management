namespace SareeStore.Application.DTOs.Products;

public class UpdateProductRequest
{
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string Fabric { get; set; } = string.Empty;
    public string WeaveType { get; set; } = string.Empty;
    public string ZariType { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = string.Empty;
    public decimal SareeLengthMeters { get; set; }
    public decimal SareeWidthInches { get; set; }
    public bool BlouseIncluded { get; set; }
    public decimal? BlouseLengthMeters { get; set; }
    public string? CraftDetails { get; set; }
    public string? Occasion { get; set; }
    public bool Has3DModel { get; set; }
    public bool IsActive { get; set; }
    public List<int>? CollectionIds { get; set; }
}
