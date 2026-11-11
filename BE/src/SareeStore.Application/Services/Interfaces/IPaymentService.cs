using SareeStore.Application.DTOs.Payment;

namespace SareeStore.Application.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentInitiateResponse> InitiatePaymentAsync(
        int customerId,
        int orderId,
        InitiatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentVerifyResponse> VerifyPaymentAsync(
        int customerId,
        int paymentId,
        VerifyPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentHistoryResponse>> GetOrderPaymentsAsync(
        int customerId,
        int orderId,
        bool isEmployeeOrAdmin,
        CancellationToken cancellationToken = default);
}
