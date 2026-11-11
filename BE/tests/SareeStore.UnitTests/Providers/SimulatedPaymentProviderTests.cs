using SareeStore.Application.Common.Exceptions;
using SareeStore.Application.Common.Models.Payment;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.PaymentProviders;
using Xunit;

namespace SareeStore.UnitTests.Providers;

public class SimulatedPaymentProviderTests
{
    private readonly SimulatedPaymentProvider _provider = new();

    [Fact]
    public void ProviderName_ShouldBeSimulated()
    {
        Assert.Equal("Simulated", _provider.ProviderName);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithNormalRequest_ShouldGenerateUniqueReference()
    {
        // Arrange
        var request1 = new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1500m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "key-1"
        };
        var request2 = new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 11,
            Amount = 1500m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI,
            IdempotencyKey = "key-2"
        };

        // Act
        var result1 = await _provider.InitiatePaymentAsync(request1);
        var result2 = await _provider.InitiatePaymentAsync(request2);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.StartsWith("SIM-", result1.ProviderPaymentReference);
        Assert.StartsWith("SIM-", result2.ProviderPaymentReference);
        Assert.NotEqual(result1.ProviderPaymentReference, result2.ProviderPaymentReference);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithInitiateDeclineAction_ShouldReturnUnsuccessfulResult()
    {
        // Arrange
        var request = new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1500m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.CreditCard,
            SimulationAction = "InitiateDecline"
        };

        // Act
        var result = await _provider.InitiatePaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("declined", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithInitiateCrashAction_ShouldThrowException()
    {
        // Arrange
        var request = new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1500m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.CreditCard,
            SimulationAction = "InitiateCrash"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.InitiatePaymentAsync(request));
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithUnknownReference_ShouldReturnFailedResult()
    {
        // Arrange
        var request = new PaymentVerifyRequest
        {
            ProviderPaymentReference = "UNKNOWN-NONEXISTENT-REF",
            ExpectedAmount = 1000m,
            ExpectedCurrency = "INR"
        };

        // Act
        var result = await _provider.VerifyPaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid or unknown payment reference", result.FailureReason);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithAmountMismatch_ShouldReturnFailedResult()
    {
        // Arrange
        var init = await _provider.InitiatePaymentAsync(new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1000m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI
        });

        var request = new PaymentVerifyRequest
        {
            ProviderPaymentReference = init.ProviderPaymentReference,
            ExpectedAmount = 2000m, // Mismatch from 1000m
            ExpectedCurrency = "INR"
        };

        // Act
        var result = await _provider.VerifyPaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Payment amount or currency mismatch.", result.FailureReason);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithSuccessfulSession_ShouldConfirmPayment()
    {
        // Arrange
        var init = await _provider.InitiatePaymentAsync(new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1000m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI,
            SimulationAction = "Success"
        });

        var request = new PaymentVerifyRequest
        {
            ProviderPaymentReference = init.ProviderPaymentReference,
            ExpectedAmount = 1000m,
            ExpectedCurrency = "INR"
        };

        // Act
        var result = await _provider.VerifyPaymentAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(init.ProviderPaymentReference, result.ProviderPaymentReference);
        Assert.NotNull(result.TransactionId);
        Assert.Equal(1000m, result.ConfirmedAmount);
        Assert.Equal("INR", result.Currency);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithFailedSimulationAction_ShouldReturnFailure()
    {
        // Arrange
        var init = await _provider.InitiatePaymentAsync(new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1000m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI,
            SimulationAction = "Failed"
        });

        var request = new PaymentVerifyRequest
        {
            ProviderPaymentReference = init.ProviderPaymentReference,
            ExpectedAmount = 1000m,
            ExpectedCurrency = "INR"
        };

        // Act
        var result = await _provider.VerifyPaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Card declined by issuing bank", result.FailureReason);
    }

    [Fact]
    public async Task VerifyPaymentAsync_AfterApplicationRestart_ShouldRecoverAndVerifySuccessfully()
    {
        // Arrange: initiate payment session
        var init = await _provider.InitiatePaymentAsync(new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 25,
            Amount = 3000m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.NetBanking
        });

        // Simulate server process restart: wipe in-memory sessions cache completely
        SimulatedPaymentProvider.ResetSessionsForTesting();

        var request = new PaymentVerifyRequest
        {
            ProviderPaymentReference = init.ProviderPaymentReference,
            ExpectedAmount = 3000m,
            ExpectedCurrency = "INR"
        };

        // Act: verify using only the self-verifying cryptographic reference
        var result = await _provider.VerifyPaymentAsync(request);

        // Assert: successfully verified without in-memory cache!
        Assert.True(result.IsSuccess);
        Assert.Equal(3000m, result.ConfirmedAmount);
        Assert.Equal("INR", result.Currency);
        Assert.NotNull(result.TransactionId);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithSamePaymentIdentity_ShouldGenerateIdenticalReference()
    {
        // Arrange
        var req1 = new PaymentInitiateRequest
        {
            OrderId = 5,
            PaymentId = 42,
            Amount = 1800m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI
        };
        var req2 = new PaymentInitiateRequest
        {
            OrderId = 5,
            PaymentId = 42,
            Amount = 1800m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.UPI
        };

        // Act
        var res1 = await _provider.InitiatePaymentAsync(req1);
        var res2 = await _provider.InitiatePaymentAsync(req2);

        // Assert
        Assert.Equal(res1.ProviderPaymentReference, res2.ProviderPaymentReference);
    }

    [Fact]
    public async Task InitiatePaymentAsync_WithInitiateTimeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var request = new PaymentInitiateRequest
        {
            OrderId = 1,
            PaymentId = 10,
            Amount = 1500m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.CreditCard,
            SimulationAction = "InitiateTimeout"
        };

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            _provider.InitiatePaymentAsync(request));
    }
}
