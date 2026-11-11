using NSubstitute;
using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Payment;
using SareeStore.Application.DTOs.Payment;
using SareeStore.Application.Services;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using Xunit;

namespace SareeStore.UnitTests.Services;

public class PaymentServiceTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IPaymentProvider _paymentProvider = Substitute.For<IPaymentProvider>();
    private readonly InitiatePaymentRequestValidator _initiateValidator = new();
    private readonly VerifyPaymentRequestValidator _verifyValidator = new();
    private readonly PaymentService _paymentService;

    public PaymentServiceTests()
    {
        _paymentProvider.ProviderName.Returns("Simulated");
        _paymentService = new PaymentService(
            _paymentRepository,
            _paymentProvider,
            _initiateValidator,
            _verifyValidator);
    }

    private static Order CreateValidPlacedOrder(int orderId = 1, int customerId = 10, decimal totalAmount = 2500m, DateTime? orderDateUtc = null)
    {
        return new Order
        {
            Id = orderId,
            CustomerId = customerId,
            OrderStatus = OrderStatus.Placed,
            PaymentStatus = PaymentStatus.Pending,
            TotalAmount = totalAmount,
            OrderDateUtc = orderDateUtc ?? DateTime.UtcNow,
            Payments = new List<Payment>
            {
                new Payment
                {
                    Id = 1,
                    OrderId = orderId,
                    PaymentMethod = PaymentMethod.UPI,
                    PaymentStatus = PaymentStatus.Pending,
                    Amount = totalAmount
                }
            }
        };
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithValidOnlineRequest_ShouldReturnProcessingPayment()
    {
        // Arrange
        const int customerId = 10;
        const int orderId = 1;
        var order = CreateValidPlacedOrder(orderId, customerId);
        var request = new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "key-001"
        };

        _paymentRepository.GetOrderForPaymentAsync(orderId, Arg.Any<CancellationToken>())
            .Returns(order);
        _paymentRepository.GetByIdempotencyKeyAsync(orderId, "key-001", Arg.Any<CancellationToken>())
            .Returns((Payment?)null);
        _paymentRepository.GetActiveProcessingPaymentAsync(orderId, Arg.Any<CancellationToken>())
            .Returns((Payment?)null);
        _paymentRepository.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var p = call.Arg<Payment>();
                p.Id = 101;
                return p;
            });

        _paymentProvider.InitiatePaymentAsync(Arg.Any<PaymentInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentInitiateResult
            {
                IsSuccess = true,
                ProviderName = "Simulated",
                ProviderPaymentReference = "SIM-TXN-001",
                InitialStatus = PaymentStatus.Processing
            });

        // Act
        var result = await _paymentService.InitiatePaymentAsync(customerId, orderId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(101, result.PaymentId);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal("Processing", result.Status);
        Assert.Equal("SIM-TXN-001", result.PaymentReference);
        await _paymentRepository.Received(1).AddAsync(Arg.Is<Payment>(p => p.PaymentStatus == PaymentStatus.Processing), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p => p.TransactionId == "SIM-TXN-001"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenOrderBelongsToDifferentCustomer_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 99);
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _paymentService.InitiatePaymentAsync(customerId: 10, orderId: 1, request));
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenOrderNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _paymentRepository.GetOrderForPaymentAsync(999, Arg.Any<CancellationToken>()).Returns((Order?)null);
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _paymentService.InitiatePaymentAsync(10, 999, request));
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenOrderIsCOD_ShouldThrowValidationException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        order.Payments.Clear();
        order.Payments.Add(new Payment { PaymentMethod = PaymentMethod.COD });
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.CreditCard };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.Contains("Cash on Delivery orders cannot be processed through online payment initiation", ex.Message);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenPaymentMethodInRequestIsCOD_ShouldThrowValidationException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.COD };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.True(ex.Errors.ContainsKey("PaymentMethod") || ex.Message.Contains("Cash on Delivery"));
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenOrderIsCancelled_ShouldThrowValidationException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        order.OrderStatus = OrderStatus.Cancelled;
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.Contains("Cannot initiate payment for a cancelled order", ex.Message);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenOrderAlreadyPaid_ShouldThrowConflictException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        order.PaymentStatus = PaymentStatus.Paid;
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenPaymentWindowExpired_ShouldThrowValidationException()
    {
        // Arrange
        var expiredOrder = CreateValidPlacedOrder(orderDateUtc: DateTime.UtcNow.AddMinutes(-31));
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(expiredOrder);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.Contains("payment window for this order has expired", ex.Message);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenActiveProcessingAttemptExists_ShouldThrowConflictException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetActiveProcessingPaymentAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Payment { Id = 50, OrderId = 1, PaymentStatus = PaymentStatus.Processing });

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenDuplicateIdempotencyKeyWithSamePayload_ShouldReturnExistingPayment()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var existingPayment = new Payment
        {
            Id = 88,
            OrderId = 1,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            PaymentProvider = "Simulated",
            TransactionId = "SIM-EXISTING-88",
            IdempotencyKey = "key-repeat"
        };
        var request = new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "key-repeat"
        };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetByIdempotencyKeyAsync(1, "key-repeat", Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        // Act
        var result = await _paymentService.InitiatePaymentAsync(10, 1, request);

        // Assert
        Assert.Equal(88, result.PaymentId);
        Assert.Equal("SIM-EXISTING-88", result.PaymentReference);
        await _paymentRepository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenDuplicateIdempotencyKeyWithDifferentPayload_ShouldThrowConflictException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var existingPayment = new Payment
        {
            Id = 88,
            OrderId = 1,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            IdempotencyKey = "key-conflict"
        };
        var request = new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.CreditCard, // Mismatch from UPI
            IdempotencyKey = "key-conflict"
        };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetByIdempotencyKeyAsync(1, "key-conflict", Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.Contains("already used with a different payment method", ex.Message);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenProviderDeclinesInitiation_ShouldMarkPaymentFailedAndThrow()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetActiveProcessingPaymentAsync(1, Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _paymentRepository.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Payment>());

        _paymentProvider.InitiatePaymentAsync(Arg.Any<PaymentInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentInitiateResult
            {
                IsSuccess = false,
                ErrorMessage = "Payment gateway declined initiation"
            });

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));

        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p =>
            p.PaymentStatus == PaymentStatus.Failed &&
            p.FailureReason == "Payment gateway declined initiation"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenVerificationSucceeds_ShouldUpdateOrderAndPaymentToPaid()
    {
        // Arrange
        const int paymentId = 55;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10);
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = "SIM-REF-55",
            Amount = 2500m,
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        _paymentProvider.VerifyPaymentAsync(Arg.Any<PaymentVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentVerifyResult
            {
                IsSuccess = true,
                ProviderPaymentReference = "SIM-REF-55",
                TransactionId = "SIM-TXN-CONFIRMED",
                ConfirmedAmount = 2500m,
                Currency = "INR"
            });

        var paidPayment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentStatus = PaymentStatus.Paid,
            TransactionId = "SIM-TXN-CONFIRMED",
            PaidAtUtc = DateTime.UtcNow
        };
        var confirmedOrder = new Order
        {
            Id = 1,
            CustomerId = 10,
            OrderStatus = OrderStatus.Confirmed,
            PaymentStatus = PaymentStatus.Paid
        };

        _paymentRepository.ProcessSuccessfulPaymentAsync(paymentId, "SIM-TXN-CONFIRMED", Arg.Any<CancellationToken>())
            .Returns((paidPayment, confirmedOrder));

        var request = new VerifyPaymentRequest { PaymentReference = "SIM-REF-55" };

        // Act
        var result = await _paymentService.VerifyPaymentAsync(customerId: 10, paymentId: paymentId, request: request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Paid", result.Status);
        Assert.Equal("Confirmed", result.OrderStatus);
        Assert.Equal("SIM-TXN-CONFIRMED", result.TransactionId);
        await _paymentRepository.Received(1).ProcessSuccessfulPaymentAsync(paymentId, "SIM-TXN-CONFIRMED", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenVerificationFails_ShouldMarkPaymentFailedAndKeepOrderPlaced()
    {
        // Arrange
        const int paymentId = 66;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10);
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = "SIM-REF-66",
            Amount = 2500m,
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        _paymentProvider.VerifyPaymentAsync(Arg.Any<PaymentVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentVerifyResult
            {
                IsSuccess = false,
                ProviderPaymentReference = "SIM-REF-66",
                FailureReason = "Payment declined by customer bank."
            });

        var request = new VerifyPaymentRequest { PaymentReference = "SIM-REF-66" };

        // Act
        var result = await _paymentService.VerifyPaymentAsync(customerId: 10, paymentId: paymentId, request: request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("Placed", result.OrderStatus);
        Assert.Equal("Payment declined by customer bank.", result.FailureReason);
        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p =>
            p.PaymentStatus == PaymentStatus.Failed &&
            p.FailureReason == "Payment declined by customer bank."), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().ProcessSuccessfulPaymentAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenDuplicateVerificationOnAlreadyPaidPayment_ShouldReturnExistingResultIdempotently()
    {
        // Arrange
        const int paymentId = 77;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10);
        order.OrderStatus = OrderStatus.Confirmed;
        order.PaymentStatus = PaymentStatus.Paid;

        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentStatus = PaymentStatus.Paid,
            TransactionId = "SIM-TXN-ALREADY-PAID",
            PaidAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        var request = new VerifyPaymentRequest { PaymentReference = "SIM-TXN-ALREADY-PAID" };

        // Act
        var result = await _paymentService.VerifyPaymentAsync(10, paymentId, request);

        // Assert
        Assert.Equal("Paid", result.Status);
        Assert.Equal("Confirmed", result.OrderStatus);
        await _paymentProvider.DidNotReceive().VerifyPaymentAsync(Arg.Any<PaymentVerifyRequest>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().ProcessSuccessfulPaymentAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenLatePaymentSuccessOnCancelledOrder_ShouldRecordPaidWithoutReactivatingOrder()
    {
        // Arrange
        const int paymentId = 88;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10);
        order.OrderStatus = OrderStatus.Cancelled;

        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = "SIM-REF-88",
            Amount = 2500m,
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        _paymentProvider.VerifyPaymentAsync(Arg.Any<PaymentVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentVerifyResult
            {
                IsSuccess = true,
                ProviderPaymentReference = "SIM-REF-88",
                TransactionId = "SIM-TXN-LATE"
            });

        var request = new VerifyPaymentRequest { PaymentReference = "SIM-REF-88" };

        // Act
        var result = await _paymentService.VerifyPaymentAsync(10, paymentId, request);

        // Assert
        Assert.Equal("Paid", result.Status);
        Assert.Equal("Cancelled", result.OrderStatus); // Order remains Cancelled!
        Assert.True(result.IsLateSuccess);
        Assert.True(result.RequiresRefund);
        Assert.Null(result.FailureReason); // Cleanly separated from FailureReason
        Assert.Contains("LATE_SUCCESS_AFTER_CANCELLATION", result.RefundReason);
        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p =>
            p.PaymentStatus == PaymentStatus.Paid &&
            p.IsLateSuccess &&
            p.RequiresRefund &&
            p.FailureReason == null &&
            p.RefundReason != null &&
            p.RefundReason.Contains("LATE_SUCCESS_AFTER_CANCELLATION")), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().ProcessSuccessfulPaymentAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenLatePaymentSuccessAfterWindowExpiry_ShouldRecordPaidWithoutConfirmingOrder()
    {
        // Arrange
        const int paymentId = 90;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10, orderDateUtc: DateTime.UtcNow.AddMinutes(-35));
        order.OrderStatus = OrderStatus.Placed;

        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = "SIM-REF-90",
            Amount = 2500m,
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        _paymentProvider.VerifyPaymentAsync(Arg.Any<PaymentVerifyRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentVerifyResult
            {
                IsSuccess = true,
                ProviderPaymentReference = "SIM-REF-90",
                TransactionId = "SIM-TXN-EXPIRED-SUCCESS"
            });

        var request = new VerifyPaymentRequest { PaymentReference = "SIM-REF-90" };

        // Act
        var result = await _paymentService.VerifyPaymentAsync(10, paymentId, request);

        // Assert
        Assert.Equal("Paid", result.Status);
        Assert.Equal("Placed", result.OrderStatus); // Order is NOT confirmed!
        Assert.True(result.IsLateSuccess);
        Assert.True(result.RequiresRefund);
        Assert.Null(result.FailureReason);
        Assert.Contains("LATE_SUCCESS_AFTER_EXPIRY", result.RefundReason);
        await _paymentRepository.DidNotReceive().ProcessSuccessfulPaymentAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenProviderTimesOut_ShouldKeepProcessingAndThrowConflictException()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var request = new InitiatePaymentRequest { PaymentMethod = PaymentMethod.UPI };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetActiveProcessingPaymentAsync(1, Arg.Any<CancellationToken>()).Returns((Payment?)null);
        _paymentRepository.AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Payment>());

        _paymentProvider.InitiatePaymentAsync(Arg.Any<PaymentInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<PaymentInitiateResult>>(_ => Task.FromException<PaymentInitiateResult>(new TimeoutException("Simulated gateway network timeout")));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _paymentService.InitiatePaymentAsync(10, 1, request));
        Assert.Contains("timed out", ex.Message);

        // Payment status must remain Processing, not prematurely marked Failed
        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p =>
            p.PaymentStatus == PaymentStatus.Processing &&
            p.FailureReason != null &&
            p.FailureReason.Contains("PROVIDER_INITIATION_TIMEOUT")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiatePaymentAsync_WhenRetriedAfterMissingTransactionId_ShouldRecoverProviderReference()
    {
        // Arrange
        var order = CreateValidPlacedOrder();
        var existingPayment = new Payment
        {
            Id = 99,
            OrderId = 1,
            PaymentMethod = PaymentMethod.UPI,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = null, // Missing reference from prior crash
            IdempotencyKey = "key-recover"
        };

        var request = new InitiatePaymentRequest
        {
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "key-recover"
        };

        _paymentRepository.GetOrderForPaymentAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _paymentRepository.GetByIdempotencyKeyAsync(1, "key-recover", Arg.Any<CancellationToken>())
            .Returns(existingPayment);

        _paymentProvider.InitiatePaymentAsync(Arg.Any<PaymentInitiateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentInitiateResult
            {
                IsSuccess = true,
                ProviderName = "Simulated",
                ProviderPaymentReference = "SIM-RECOVERED-99"
            });

        // Act
        var result = await _paymentService.InitiatePaymentAsync(10, 1, request);

        // Assert
        Assert.Equal(99, result.PaymentId);
        Assert.Equal("SIM-RECOVERED-99", result.PaymentReference);
        await _paymentRepository.Received(1).UpdateAsync(Arg.Is<Payment>(p => p.TransactionId == "SIM-RECOVERED-99"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyPaymentAsync_WhenPaymentReferenceMismatches_ShouldThrowValidationException()
    {
        // Arrange
        const int paymentId = 99;
        var order = CreateValidPlacedOrder(orderId: 1, customerId: 10);
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = 1,
            PaymentStatus = PaymentStatus.Processing,
            TransactionId = "SIM-REF-EXPECTED",
            Order = order
        };

        _paymentRepository.GetByIdWithOrderAsync(paymentId, Arg.Any<CancellationToken>()).Returns(payment);
        var request = new VerifyPaymentRequest { PaymentReference = "SIM-REF-DIFFERENT" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _paymentService.VerifyPaymentAsync(10, paymentId, request));
        Assert.Contains("payment reference does not match", ex.Message);
    }
}
