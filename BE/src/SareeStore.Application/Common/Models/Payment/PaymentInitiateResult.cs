using SareeStore.Domain.Enums;

namespace SareeStore.Application.Common.Models.Payment;

public class PaymentInitiateResult
{
    public bool IsSuccess { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderPaymentReference { get; set; } = string.Empty;
    public string? ProviderSessionData { get; set; }
    public PaymentStatus InitialStatus { get; set; } = PaymentStatus.Processing;
    public DateTime? ExpiresAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
