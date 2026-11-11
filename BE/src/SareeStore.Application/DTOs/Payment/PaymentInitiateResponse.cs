namespace SareeStore.Application.DTOs.Payment;

public class PaymentInitiateResponse
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
}
