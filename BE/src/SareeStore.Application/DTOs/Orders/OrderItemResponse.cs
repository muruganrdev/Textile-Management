namespace SareeStore.Application.DTOs.Orders;

public class OrderItemResponse
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string? PrimaryImageUrl { get; set; }
}
