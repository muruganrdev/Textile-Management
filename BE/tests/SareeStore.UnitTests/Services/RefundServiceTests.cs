using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Refund;
using SareeStore.Application.Services;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;

namespace SareeStore.UnitTests.Services;

public class RefundServiceTests
{
    private readonly IRefundRepository _refundRepository = Substitute.For<IRefundRepository>();
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IRefundProvider _refundProvider = Substitute.For<IRefundProvider>();
    private readonly RefundService _refundService;

    public RefundServiceTests()
    {
        _refundService = new RefundService(
            _refundRepository,
            _paymentRepository,
            _refundProvider);
    }

    [Fact]
    public async Task InitiateRefundForOrderCancellationAsync_WhenAlreadyInitiated_ReturnsExistingIdempotently()
    {
        const int orderId = 10;
        const int paymentId = 20;
        const string idempotencyKey = "REF-ORD10-PAY20-CANCEL";

        var existingRefund = new Refund
        {
            Id = 5,
            OrderId = orderId,
            PaymentId = paymentId,
            Amount = 4500m,
            Currency = "INR",
            Status = RefundStatus.Success,
            RefundReference = "SIM-REF-123",
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        };

        _refundRepository.GetByIdempotencyKeyAsync(paymentId, idempotencyKey, Arg.Any<CancellationToken>())
            .Returns(existingRefund);

        var response = await _refundService.InitiateRefundForOrderCancellationAsync(
            orderId, paymentId, 4500m, "Customer cancellation");

        Assert.Equal(5, response.Id);
        Assert.Equal("Success", response.Status);
        Assert.Equal("SIM-REF-123", response.RefundReference);
        await _refundProvider.DidNotReceiveWithAnyArgs().InitiateRefundAsync(default!);
    }

    [Fact]
    public async Task InitiateRefundForOrderCancellationAsync_WhenProviderSucceeds_MarksSuccess()
    {
        const int orderId = 10;
        const int paymentId = 20;
        const decimal amount = 5000m;

        _paymentRepository.GetByIdAsync(paymentId, Arg.Any<CancellationToken>())
            .Returns(new Payment { Id = paymentId, OrderId = orderId, Amount = amount, TransactionId = "SIM-TXN-123" });

        _refundRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Refund
            {
                Id = 1,
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = amount,
                Currency = "INR",
                Status = RefundStatus.Pending,
                Reason = "Cancelled",
                IdempotencyKey = "REF-ORD10-PAY20-CANCEL",
                CreatedAtUtc = DateTime.UtcNow
            });

        _refundProvider.InitiateRefundAsync(Arg.Any<RefundInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundInitiateResult
            {
                IsSuccess = true,
                RefundReference = "SIM-REF-ORD10-PAY20-SUCC-abc",
                Status = RefundStatus.Success
            });

        _refundRepository.ProcessSuccessfulRefundAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(info => new Refund
            {
                Id = info.ArgAt<int>(0),
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = amount,
                Currency = "INR",
                Status = RefundStatus.Success,
                RefundReference = info.ArgAt<string>(1),
                RefundedAtUtc = DateTime.UtcNow
            });

        var response = await _refundService.InitiateRefundForOrderCancellationAsync(
            orderId, paymentId, amount, "Cancelled");

        Assert.Equal("Success", response.Status);
        Assert.Equal("SIM-REF-ORD10-PAY20-SUCC-abc", response.RefundReference);
        await _refundRepository.Received(1).ProcessSuccessfulRefundAsync(Arg.Any<int>(), "SIM-REF-ORD10-PAY20-SUCC-abc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateRefundForOrderCancellationAsync_WhenProviderTimesOut_MarksProcessingWithoutFailing()
    {
        const int orderId = 10;
        const int paymentId = 20;
        const decimal amount = 5000m;

        _paymentRepository.GetByIdAsync(paymentId, Arg.Any<CancellationToken>())
            .Returns(new Payment { Id = paymentId, OrderId = orderId, Amount = amount, TransactionId = "SIM-TXN-123" });

        _refundRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Refund
            {
                Id = 2,
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = amount,
                Currency = "INR",
                Status = RefundStatus.Pending,
                Reason = "Cancelled",
                IdempotencyKey = "REF-ORD10-PAY20-CANCEL",
                CreatedAtUtc = DateTime.UtcNow
            });

        _refundProvider.InitiateRefundAsync(Arg.Any<RefundInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<RefundInitiateResult>>(_ => Task.FromException<RefundInitiateResult>(new TimeoutException("Network timeout")));

        var response = await _refundService.InitiateRefundForOrderCancellationAsync(
            orderId, paymentId, amount, "Cancelled");

        Assert.Equal("Processing", response.Status);
        Assert.Contains("REFUND_TIMEOUT", response.FailureReason);
        await _refundRepository.Received().UpdateAsync(Arg.Is<Refund>(r => r.Status == RefundStatus.Processing), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateRefundForOrderCancellationAsync_WhenProviderDeclines_MarksFailed()
    {
        const int orderId = 10;
        const int paymentId = 20;
        const decimal amount = 5000m;

        _paymentRepository.GetByIdAsync(paymentId, Arg.Any<CancellationToken>())
            .Returns(new Payment { Id = paymentId, OrderId = orderId, Amount = amount, TransactionId = "SIM-TXN-123" });

        _refundRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Refund
            {
                Id = 3,
                OrderId = orderId,
                PaymentId = paymentId,
                Amount = amount,
                Currency = "INR",
                Status = RefundStatus.Pending,
                Reason = "Cancelled",
                IdempotencyKey = "REF-ORD10-PAY20-CANCEL",
                CreatedAtUtc = DateTime.UtcNow
            });

        _refundProvider.InitiateRefundAsync(Arg.Any<RefundInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundInitiateResult
            {
                IsSuccess = false,
                RefundReference = "SIM-REF-ORD10-PAY20-FAIL-abc",
                Status = RefundStatus.Failed,
                ErrorMessage = "Acquiring bank declined refund request."
            });

        var response = await _refundService.InitiateRefundForOrderCancellationAsync(
            orderId, paymentId, amount, "Cancelled");

        Assert.Equal("Failed", response.Status);
        Assert.Contains("declined", response.FailureReason);
        await _refundRepository.Received().UpdateAsync(Arg.Is<Refund>(r => r.Status == RefundStatus.Failed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrderRefundsAsync_WhenCustomerIsNotOwner_ThrowsForbiddenException()
    {
        const int customerId = 1;
        const int orderId = 99;

        _paymentRepository.GetOrderForPaymentAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(new Order { Id = orderId, CustomerId = 2 }); // different customer

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _refundService.GetOrderRefundsAsync(customerId, orderId, isEmployeeOrAdmin: false));
    }

    [Fact]
    public async Task RetryOrReconcileRefundAsync_WhenCustomerIsNotOwner_ThrowsForbiddenException()
    {
        const int customerId = 1;
        const int refundId = 55;

        _refundRepository.GetByIdAsync(refundId, Arg.Any<CancellationToken>())
            .Returns(new Refund
            {
                Id = refundId,
                OrderId = 10,
                PaymentId = 20,
                Amount = 1000m,
                Currency = "INR",
                Status = RefundStatus.Processing,
                Order = new Order { Id = 10, CustomerId = 99 } // Different customer
            });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _refundService.RetryOrReconcileRefundAsync(customerId, refundId));
    }

    [Fact]
    public async Task RetryOrReconcileRefundAsync_WhenProcessingWithTimeout_RemainsProcessingWithoutFailing()
    {
        const int refundId = 60;
        var refund = new Refund
        {
            Id = refundId,
            OrderId = 10,
            PaymentId = 20,
            Amount = 2500m,
            Currency = "INR",
            Status = RefundStatus.Processing,
            RefundReference = "SIM-REF-ORD10-PAY20-SUCC-12345",
            Order = new Order { Id = 10, CustomerId = 1 }
        };

        _refundRepository.GetByIdAsync(refundId, Arg.Any<CancellationToken>())
            .Returns(refund);

        _refundProvider.VerifyRefundAsync(Arg.Any<RefundVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<RefundVerifyResult>>(_ => Task.FromException<RefundVerifyResult>(new TimeoutException("Gateway timeout")));

        var response = await _refundService.RetryOrReconcileRefundAsync(1, refundId);

        Assert.Equal("Processing", response.Status);
        Assert.Contains("REFUND_TIMEOUT", response.FailureReason);
        await _refundRepository.Received(1).UpdateAsync(Arg.Is<Refund>(r => r.Status == RefundStatus.Processing), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryOrReconcileRefundAsync_WhenProcessingAndVerifySucceeds_MarksSuccess()
    {
        const int refundId = 65;
        var refund = new Refund
        {
            Id = refundId,
            OrderId = 10,
            PaymentId = 20,
            Amount = 3000m,
            Currency = "INR",
            Status = RefundStatus.Processing,
            RefundReference = "SIM-REF-ORD10-PAY20-SUCC-99999",
            Order = new Order { Id = 10, CustomerId = 1 }
        };

        _refundRepository.GetByIdAsync(refundId, Arg.Any<CancellationToken>())
            .Returns(refund);

        _refundProvider.VerifyRefundAsync(Arg.Any<RefundVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new RefundVerifyResult
            {
                IsSuccess = true,
                RefundReference = refund.RefundReference,
                Status = RefundStatus.Success
            });

        _refundRepository.ProcessSuccessfulRefundAsync(refundId, refund.RefundReference, Arg.Any<CancellationToken>())
            .Returns(new Refund
            {
                Id = refundId,
                OrderId = 10,
                PaymentId = 20,
                Amount = 3000m,
                Currency = "INR",
                Status = RefundStatus.Success,
                RefundReference = refund.RefundReference,
                RefundedAtUtc = DateTime.UtcNow
            });

        var response = await _refundService.RetryOrReconcileRefundAsync(1, refundId);

        Assert.Equal("Success", response.Status);
        Assert.Equal(refund.RefundReference, response.RefundReference);
    }
}
