using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Wishlist : BaseEntity
{
    public int UserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
}
