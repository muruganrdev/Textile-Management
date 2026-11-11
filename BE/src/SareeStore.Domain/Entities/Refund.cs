using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class Refund : BaseEntity
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public string? RefundReference { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string Reason { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? RefundedAtUtc { get; set; }

    // Navigation properties
    public Order Order { get; set; } = null!;
    public Payment Payment { get; set; } = null!;
}
