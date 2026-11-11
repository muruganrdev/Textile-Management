namespace SareeStore.Application.DTOs.Payment;

public class PaymentHistoryResponse
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string PaymentStatus { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool IsLateSuccess { get; set; }
    public bool RequiresRefund { get; set; }
    public string? RefundReason { get; set; }
}
