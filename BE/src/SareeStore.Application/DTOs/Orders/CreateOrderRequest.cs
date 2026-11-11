using SareeStore.Domain.Enums;

namespace SareeStore.Application.DTOs.Orders;

public class CreateOrderRequest
{
    public int ShippingAddressId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
}
