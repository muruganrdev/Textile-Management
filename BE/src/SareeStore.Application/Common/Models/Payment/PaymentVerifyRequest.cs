namespace SareeStore.Application.Common.Models.Payment;

public class PaymentVerifyRequest
{
    public string ProviderPaymentReference { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public string ExpectedCurrency { get; set; } = "INR";
}
