using SareeStore.Domain.Enums;

namespace SareeStore.Application.Common.Models.Refund;

public class RefundInitiateResult
{
    public bool IsSuccess { get; set; }
    public string? RefundReference { get; set; }
    public RefundStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
