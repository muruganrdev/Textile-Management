using SareeStore.Application.Common.Models.Payment;

namespace SareeStore.Application.Common.Interfaces;

public interface IPaymentProvider
{
    string ProviderName { get; }
    Task<PaymentInitiateResult> InitiatePaymentAsync(PaymentInitiateRequest request, CancellationToken cancellationToken = default);
    Task<PaymentVerifyResult> VerifyPaymentAsync(PaymentVerifyRequest request, CancellationToken cancellationToken = default);
}
