using SareeStore.Domain.Enums;

namespace SareeStore.Application.Common.Models.Payment;

public class PaymentInitiateRequest
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentMethod PaymentMethod { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? SimulationAction { get; set; }
}
