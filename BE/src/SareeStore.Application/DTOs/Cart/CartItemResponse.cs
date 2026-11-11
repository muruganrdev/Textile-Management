namespace SareeStore.Application.DTOs.Cart;

public class CartItemResponse
{
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public decimal EffectiveUnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public int AvailableStock { get; set; }
    public bool IsAvailable { get; set; }
    public string? AvailabilityMessage { get; set; }
}
