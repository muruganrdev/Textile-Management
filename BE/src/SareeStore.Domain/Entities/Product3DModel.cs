using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Product3DModel : BaseEntity
{
    public int ProductId { get; set; }
    public string GlbModelUrl { get; set; } = string.Empty;
    public string? UsdzModelUrl { get; set; }
    public string? PosterImageUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public string? CameraTarget { get; set; }
    public string? CameraOrbit { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public Product Product { get; set; } = null!;
}
