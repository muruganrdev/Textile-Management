using SareeStore.Domain.Enums;

namespace SareeStore.Application.Common.Models.Refund;

public class RefundVerifyResult
{
    public bool IsSuccess { get; set; }
    public string RefundReference { get; set; } = string.Empty;
    public RefundStatus Status { get; set; }
    public string? FailureReason { get; set; }
}
