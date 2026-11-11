namespace SareeStore.Application.DTOs.Collections;

public class CollectionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
