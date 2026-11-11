using SareeStore.Domain.Common;

namespace SareeStore.Domain.Entities;

public class CartItem : BaseEntity
{
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;

    // Navigation properties
    public Cart Cart { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
