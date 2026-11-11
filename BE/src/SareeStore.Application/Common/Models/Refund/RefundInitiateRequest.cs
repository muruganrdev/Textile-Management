namespace SareeStore.Application.Common.Models.Refund;

public class RefundInitiateRequest
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string PaymentTransactionId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
