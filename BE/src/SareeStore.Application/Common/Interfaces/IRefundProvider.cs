using SareeStore.Application.Common.Models.Refund;

namespace SareeStore.Application.Common.Interfaces;

public interface IRefundProvider
{
    string ProviderName { get; }

    Task<RefundInitiateResult> InitiateRefundAsync(
        RefundInitiateRequest request,
        CancellationToken cancellationToken = default);

    Task<RefundVerifyResult> VerifyRefundAsync(
        RefundVerifyRequest request,
        CancellationToken cancellationToken = default);
}
