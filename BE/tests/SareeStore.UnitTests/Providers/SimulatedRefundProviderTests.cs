using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SareeStore.Application.Common.Models.Refund;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.PaymentProviders;

namespace SareeStore.UnitTests.Providers;

public class SimulatedRefundProviderTests
{
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly ILogger<SimulatedRefundProvider> _logger = Substitute.For<ILogger<SimulatedRefundProvider>>();

    public SimulatedRefundProviderTests()
    {
        _environment.EnvironmentName.Returns("Development");
        SimulatedRefundProvider.ResetSessionsForTesting();
    }

    [Fact]
    public async Task InitiateRefundAsync_Default_ReturnsSuccessWithDeterministicReference()
    {
        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var request = new RefundInitiateRequest
        {
            OrderId = 10,
            PaymentId = 20,
            Amount = 4500m,
            Currency = "INR",
            Reason = "Customer requested cancellation"
        };

        var result = await provider.InitiateRefundAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Success, result.Status);
        Assert.NotNull(result.RefundReference);
        Assert.StartsWith("SIM-REF-ORD10-PAY20-SUCC-", result.RefundReference);
    }

    [Fact]
    public async Task InitiateRefundAsync_WhenHeaderIsRefundDeclined_ReturnsFailed()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Simulation-Action"] = "RefundDeclined";
        _httpContextAccessor.HttpContext.Returns(context);

        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var request = new RefundInitiateRequest
        {
            OrderId = 10,
            PaymentId = 20,
            Amount = 3000m,
            Currency = "INR"
        };

        var result = await provider.InitiateRefundAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(RefundStatus.Failed, result.Status);
        Assert.Contains("declined", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitiateRefundAsync_WhenHeaderIsRefundTimeout_ThrowsTimeoutException()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Simulation-Action"] = "RefundTimeout";
        _httpContextAccessor.HttpContext.Returns(context);

        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var request = new RefundInitiateRequest
        {
            OrderId = 10,
            PaymentId = 20,
            Amount = 3000m,
            Currency = "INR"
        };

        await Assert.ThrowsAsync<TimeoutException>(() => provider.InitiateRefundAsync(request));
    }

    [Fact]
    public async Task VerifyRefundAsync_AfterSimulatedRestart_StatelesslyRecoversAndSucceeds()
    {
        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var initResult = await provider.InitiateRefundAsync(new RefundInitiateRequest
        {
            OrderId = 15,
            PaymentId = 30,
            Amount = 6000m,
            Currency = "INR"
        });

        Assert.True(initResult.IsSuccess);
        var refundRef = initResult.RefundReference!;

        // Simulate complete application restart by clearing in-memory sessions
        SimulatedRefundProvider.ResetSessionsForTesting();

        // Verify across new provider instance
        var newProvider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var verifyResult = await newProvider.VerifyRefundAsync(new RefundVerifyRequest
        {
            RefundReference = refundRef,
            ExpectedAmount = 6000m,
            ExpectedCurrency = "INR"
        });

        Assert.True(verifyResult.IsSuccess);
        Assert.Equal(RefundStatus.Success, verifyResult.Status);
        Assert.Null(verifyResult.FailureReason);
    }

    [Fact]
    public async Task VerifyRefundAsync_WhenTamperedAmount_FailsCryptographicVerification()
    {
        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var initResult = await provider.InitiateRefundAsync(new RefundInitiateRequest
        {
            OrderId = 15,
            PaymentId = 30,
            Amount = 6000m,
            Currency = "INR"
        });

        var refundRef = initResult.RefundReference!;

        // Reset session so it triggers cryptographic verification
        SimulatedRefundProvider.ResetSessionsForTesting();

        // Caller asks to verify with different amount (e.g. 9999m)
        var verifyResult = await provider.VerifyRefundAsync(new RefundVerifyRequest
        {
            RefundReference = refundRef,
            ExpectedAmount = 9999m,
            ExpectedCurrency = "INR"
        });

        Assert.False(verifyResult.IsSuccess);
        Assert.Equal(RefundStatus.Failed, verifyResult.Status);
        Assert.Contains("tampered", verifyResult.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitiateRefundAsync_InProduction_IgnoresSimulationHeader()
    {
        _environment.EnvironmentName.Returns("Production");

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Simulation-Action"] = "RefundDeclined";
        _httpContextAccessor.HttpContext.Returns(context);

        var provider = new SimulatedRefundProvider(_httpContextAccessor, _environment, _logger);
        var result = await provider.InitiateRefundAsync(new RefundInitiateRequest
        {
            OrderId = 25,
            PaymentId = 50,
            Amount = 2500m,
            Currency = "INR"
        });

        // Header must be ignored in Production, returning Success
        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Success, result.Status);
    }
}
