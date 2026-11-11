using SareeStore.Domain.Common;
using SareeStore.Domain.Enums;

namespace SareeStore.Domain.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? IdempotencyKey { get; set; }
    public string PaymentProvider { get; set; } = "Simulated";
    public bool IsLateSuccess { get; set; } = false;
    public bool RequiresRefund { get; set; } = false;
    public string? RefundReason { get; set; }

    // Navigation property
    public Order Order { get; set; } = null!;
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
