namespace SareeStore.Application.Common.Models.Refund;

public class RefundVerifyRequest
{
    public string RefundReference { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public string ExpectedCurrency { get; set; } = "INR";
}
