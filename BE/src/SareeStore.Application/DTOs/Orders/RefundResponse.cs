namespace SareeStore.Application.DTOs.Orders;

public class RefundResponse
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public string? RefundReference { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
}
