using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class Cart : BaseEntity
{
    public int UserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
