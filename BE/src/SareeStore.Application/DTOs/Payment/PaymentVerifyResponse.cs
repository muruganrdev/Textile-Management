namespace SareeStore.Application.DTOs.Payment;

public class PaymentVerifyResponse
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public bool IsLateSuccess { get; set; }
    public bool RequiresRefund { get; set; }
    public string? RefundReason { get; set; }
}
