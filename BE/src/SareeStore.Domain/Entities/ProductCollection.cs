namespace SareeStore.Domain.Entities;

public class ProductCollection
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
}
