using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class WishlistItem : BaseEntity
{
    public int WishlistId { get; set; }
    public int ProductId { get; set; }

    // Navigation properties
    public Wishlist Wishlist { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
