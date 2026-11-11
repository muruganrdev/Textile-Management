namespace SareeStore.Application.DTOs.Cart;

public class CartResponse
{
    public int CartId { get; set; }
    public IReadOnlyList<CartItemResponse> Items { get; set; } = new List<CartItemResponse>();
    public int TotalItems { get; set; }
    public decimal SubTotal { get; set; }
}
