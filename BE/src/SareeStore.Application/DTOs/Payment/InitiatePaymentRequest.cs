using SareeStore.Domain.Enums;

namespace SareeStore.Application.DTOs.Payment;

public class InitiatePaymentRequest
{
    public PaymentMethod PaymentMethod { get; set; }
    public string? IdempotencyKey { get; set; }
}
