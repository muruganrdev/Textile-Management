using SareeStore.Domain.Enums;

namespace SareeStore.Application.Common.Models.Payment;

public class PaymentVerifyResult
{
    public bool IsSuccess { get; set; }
    public string ProviderPaymentReference { get; set; } = string.Empty;
    public PaymentStatus ResultStatus { get; set; } = PaymentStatus.Processing;
    public decimal ConfirmedAmount { get; set; }
    public string Currency { get; set; } = "INR";
    public string? FailureReason { get; set; }
    public string? TransactionId { get; set; }
}
