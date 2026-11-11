namespace SareeStore.Application.DTOs.Collections;

public class CreateCollectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
}
