using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Refund;
using SareeStore.Domain.Enums;

namespace SareeStore.Infrastructure.PaymentProviders;

public class SimulatedRefundProvider : IRefundProvider
{
    private const string Salt = "SareeStore_Refund_Secret_Salt_2026_#Sec!";
    private static readonly ConcurrentDictionary<string, SimulatedRefundSession> ActiveRefundSessions = new();

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SimulatedRefundProvider> _logger;

    public SimulatedRefundProvider(
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment environment,
        ILogger<SimulatedRefundProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
        _logger = logger;
    }

    public string ProviderName => "Simulated";

    public Task<RefundInitiateResult> InitiateRefundAsync(
        RefundInitiateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var action = ResolveSimulationAction();

        _logger.LogInformation("Simulating refund initiation for Order {OrderId}, Payment {PaymentId}, Amount {Amount}. Action: {Action}",
            request.OrderId, request.PaymentId, request.Amount, action);

        if (action.Equals("RefundTimeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Simulated refund gateway gateway network timeout.");
        }

        if (action.Equals("RefundCrash", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated unexpected crash during refund gateway initiation.");
        }

        if (action.Equals("RefundDeclined", StringComparison.OrdinalIgnoreCase) ||
            action.Equals("RefundFailed", StringComparison.OrdinalIgnoreCase))
        {
            var failRef = GenerateRefundReference(request.OrderId, request.PaymentId, "FAIL", request.Amount, request.Currency);
            ActiveRefundSessions[failRef] = new SimulatedRefundSession(failRef, request.Amount, request.Currency, "FAIL");

            return Task.FromResult(new RefundInitiateResult
            {
                IsSuccess = false,
                RefundReference = failRef,
                Status = RefundStatus.Failed,
                ErrorMessage = "Refund request declined by acquiring bank."
            });
        }

        // Default: Success
        var succRef = GenerateRefundReference(request.OrderId, request.PaymentId, "SUCC", request.Amount, request.Currency);
        ActiveRefundSessions[succRef] = new SimulatedRefundSession(succRef, request.Amount, request.Currency, "SUCC");

        return Task.FromResult(new RefundInitiateResult
        {
            IsSuccess = true,
            RefundReference = succRef,
            Status = RefundStatus.Success,
            ErrorMessage = null
        });
    }

    public Task<RefundVerifyResult> VerifyRefundAsync(
        RefundVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefundReference))
        {
            return Task.FromResult(new RefundVerifyResult
            {
                IsSuccess = false,
                RefundReference = request.RefundReference,
                Status = RefundStatus.Failed,
                FailureReason = "Refund reference cannot be null or empty."
            });
        }

        // 1. Try fast-path cache
        if (ActiveRefundSessions.TryGetValue(request.RefundReference, out var session))
        {
            if (session.ActionToken == "FAIL")
            {
                return Task.FromResult(new RefundVerifyResult
                {
                    IsSuccess = false,
                    RefundReference = request.RefundReference,
                    Status = RefundStatus.Failed,
                    FailureReason = "Refund declined by issuing bank."
                });
            }

            return Task.FromResult(new RefundVerifyResult
            {
                IsSuccess = true,
                RefundReference = request.RefundReference,
                Status = RefundStatus.Success,
                FailureReason = null
            });
        }

        // 2. Stateless reconstruction across application restarts
        var (isValid, orderId, paymentId, actionToken) = TryParseAndVerifyReference(
            request.RefundReference, request.ExpectedAmount, request.ExpectedCurrency);

        if (!isValid)
        {
            _logger.LogWarning("Invalid or tampered refund reference provided: {Reference}", request.RefundReference);
            return Task.FromResult(new RefundVerifyResult
            {
                IsSuccess = false,
                RefundReference = request.RefundReference,
                Status = RefundStatus.Failed,
                FailureReason = "Invalid or tampered refund reference."
            });
        }

        if (actionToken == "FAIL")
        {
            return Task.FromResult(new RefundVerifyResult
            {
                IsSuccess = false,
                RefundReference = request.RefundReference,
                Status = RefundStatus.Failed,
                FailureReason = "Refund declined by issuing bank."
            });
        }

        return Task.FromResult(new RefundVerifyResult
        {
            IsSuccess = true,
            RefundReference = request.RefundReference,
            Status = RefundStatus.Success,
            FailureReason = null
        });
    }

    public static void ResetSessionsForTesting()
    {
        ActiveRefundSessions.Clear();
    }

    private string ResolveSimulationAction()
    {
        if (!_environment.IsDevelopment())
        {
            return "RefundSuccess";
        }

        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers["X-Simulation-Action"].ToString();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.Trim();
        }

        return "RefundSuccess";
    }

    private static string GenerateRefundReference(int orderId, int paymentId, string actionToken, decimal amount, string currency)
    {
        var signature = ComputeSignature(orderId, paymentId, actionToken, amount, currency);
        return $"SIM-REF-ORD{orderId}-PAY{paymentId}-{actionToken}-{signature}";
    }

    private static (bool IsValid, int OrderId, int PaymentId, string ActionToken) TryParseAndVerifyReference(
        string reference, decimal expectedAmount, string expectedCurrency)
    {
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("SIM-REF-ORD", StringComparison.OrdinalIgnoreCase))
        {
            return (false, 0, 0, string.Empty);
        }

        try
        {
            var parts = reference.Split('-');
            // SIM, REF, ORD{orderId}, PAY{paymentId}, {actionToken}, {signature}
            if (parts.Length != 6)
            {
                return (false, 0, 0, string.Empty);
            }

            var ordPart = parts[2];
            var payPart = parts[3];
            var actionToken = parts[4];
            var providedSignature = parts[5];

            if (!ordPart.StartsWith("ORD", StringComparison.OrdinalIgnoreCase) ||
                !payPart.StartsWith("PAY", StringComparison.OrdinalIgnoreCase))
            {
                return (false, 0, 0, string.Empty);
            }

            if (!int.TryParse(ordPart[3..], out var orderId) ||
                !int.TryParse(payPart[3..], out var paymentId))
            {
                return (false, 0, 0, string.Empty);
            }

            var expectedSignature = ComputeSignature(orderId, paymentId, actionToken, expectedAmount, expectedCurrency);
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedSignature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            {
                return (false, 0, 0, string.Empty);
            }

            return (true, orderId, paymentId, actionToken);
        }
        catch
        {
            return (false, 0, 0, string.Empty);
        }
    }

    private static string ComputeSignature(int orderId, int paymentId, string actionToken, decimal amount, string currency)
    {
        var payload = $"{orderId}:{paymentId}:{actionToken}:{amount:F2}:{currency.ToUpperInvariant()}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Salt));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private sealed record SimulatedRefundSession(string Reference, decimal Amount, string Currency, string ActionToken);
}
