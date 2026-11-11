using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Application.Common.Models.Payment;
using SareeStore.Domain.Enums;

namespace SareeStore.Infrastructure.PaymentProviders;

/// <summary>
/// Hardened simulated payment provider implementing deterministic, tamper-proof, restart-recoverable
/// payment simulation for demo and testing environments.
/// Does not integrate with live external gateways.
/// </summary>
public class SimulatedPaymentProvider : IPaymentProvider
{
    public const string SimulationHeaderName = "X-Simulation-Action";
    private const string ProviderSecretSalt = "SareeStore-Simulated-Provider-Secret-2026";

    public string ProviderName => "Simulated";

    private readonly IHostEnvironment? _environment;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    // In-memory cache for ultra-fast lookup within process lifetime
    private static readonly ConcurrentDictionary<string, SimulatedSession> Sessions = new();

    public SimulatedPaymentProvider(
        IHostEnvironment? environment = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
    }

    public sealed class SimulatedSession
    {
        public int OrderId { get; init; }
        public int PaymentId { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "INR";
        public string SimulationAction { get; init; } = "Success";
        public DateTime CreatedAtUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
    }

    /// <summary>
    /// Clears the in-memory cache to simulate an application restart or multi-instance cache miss during testing.
    /// </summary>
    public static void ResetSessionsForTesting()
    {
        Sessions.Clear();
    }

    public Task<PaymentInitiateResult> InitiatePaymentAsync(
        PaymentInitiateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Determine simulation action securely (ignoring client header in production)
        var action = ResolveSimulationAction(request);

        // 2. Ambiguous or definitive initiation failure simulation
        if (string.Equals(action, "InitiateCrash", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated provider gateway connectivity exception.");
        }

        if (string.Equals(action, "InitiateTimeout", StringComparison.OrdinalIgnoreCase))
        {
            throw new TimeoutException("Simulated gateway network timeout during initiation.");
        }

        if (string.Equals(action, "InitiateDecline", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new PaymentInitiateResult
            {
                IsSuccess = false,
                ProviderName = ProviderName,
                ErrorMessage = "Payment provider declined initiation request for this instrument."
            });
        }

        // 3. Map action to normalized token for the tamper-proof reference
        var actionToken = NormalizeActionToken(action);

        // 4. Generate deterministic, self-verifying, tamper-proof provider reference
        // Format: SIM-ORD{orderId}-PAY{paymentId}-{actionToken}-{signature}
        var signature = ComputeSignature(request.OrderId, request.PaymentId, request.Amount, request.Currency, actionToken);
        var reference = $"SIM-ORD{request.OrderId}-PAY{request.PaymentId}-{actionToken}-{signature}";

        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(30);

        var session = new SimulatedSession
        {
            OrderId = request.OrderId,
            PaymentId = request.PaymentId,
            Amount = request.Amount,
            Currency = request.Currency,
            SimulationAction = action,
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt
        };

        Sessions[reference] = session;

        return Task.FromResult(new PaymentInitiateResult
        {
            IsSuccess = true,
            ProviderName = ProviderName,
            ProviderPaymentReference = reference,
            ProviderSessionData = $"{{\"orderId\":{request.OrderId},\"paymentId\":{request.PaymentId},\"amount\":{request.Amount}}}",
            InitialStatus = PaymentStatus.Processing,
            ExpiresAtUtc = expiresAt
        });
    }

    public Task<PaymentVerifyResult> VerifyPaymentAsync(
        PaymentVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Try fast in-memory resolution first
        if (Sessions.TryGetValue(request.ProviderPaymentReference, out var session))
        {
            return Task.FromResult(VerifySession(request, session.Amount, session.Currency, session.SimulationAction));
        }

        // 2. RESTART RECOVERY / MULTI-INSTANCE RESOLUTION:
        // Parse and cryptographically verify reference: SIM-ORD{orderId}-PAY{paymentId}-{actionToken}-{signature}
        if (!TryParseAndVerifyReference(request.ProviderPaymentReference, request.ExpectedAmount, request.ExpectedCurrency, out var recoveredAction))
        {
            // Check if reference format is valid but amount mismatched
            if (TryParseReferenceParts(request.ProviderPaymentReference, out var orderId, out var paymentId, out var actToken, out var sig))
            {
                return Task.FromResult(new PaymentVerifyResult
                {
                    IsSuccess = false,
                    ProviderPaymentReference = request.ProviderPaymentReference,
                    ResultStatus = PaymentStatus.Failed,
                    FailureReason = "Payment amount or currency mismatch."
                });
            }

            return Task.FromResult(new PaymentVerifyResult
            {
                IsSuccess = false,
                ProviderPaymentReference = request.ProviderPaymentReference,
                ResultStatus = PaymentStatus.Failed,
                FailureReason = "Invalid or unknown payment reference."
            });
        }

        // Successfully recovered from cryptographic token across application restart or multi-instance node
        return Task.FromResult(VerifySession(request, request.ExpectedAmount, request.ExpectedCurrency, recoveredAction));
    }

    private PaymentVerifyResult VerifySession(
        PaymentVerifyRequest request,
        decimal expectedAmount,
        string expectedCurrency,
        string action)
    {
        if (expectedAmount != request.ExpectedAmount ||
            !string.Equals(expectedCurrency, request.ExpectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentVerifyResult
            {
                IsSuccess = false,
                ProviderPaymentReference = request.ProviderPaymentReference,
                ResultStatus = PaymentStatus.Failed,
                FailureReason = "Payment amount or currency mismatch."
            };
        }

        if (string.Equals(action, "Failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Declined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "FAIL_DECLINED", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentVerifyResult
            {
                IsSuccess = false,
                ProviderPaymentReference = request.ProviderPaymentReference,
                ResultStatus = PaymentStatus.Failed,
                FailureReason = "Card declined by issuing bank."
            };
        }

        if (string.Equals(action, "InsufficientFunds", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "FAIL_FUNDS", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentVerifyResult
            {
                IsSuccess = false,
                ProviderPaymentReference = request.ProviderPaymentReference,
                ResultStatus = PaymentStatus.Failed,
                FailureReason = "Insufficient funds in customer account."
            };
        }

        // Default successful outcome
        var transactionId = $"SIM-TXN-{request.ProviderPaymentReference.Replace("SIM-", "")}";

        return new PaymentVerifyResult
        {
            IsSuccess = true,
            ProviderPaymentReference = request.ProviderPaymentReference,
            ResultStatus = PaymentStatus.Paid,
            ConfirmedAmount = expectedAmount,
            Currency = expectedCurrency,
            TransactionId = transactionId
        };
    }

    private string ResolveSimulationAction(PaymentInitiateRequest request)
    {
        // Check if environment is Development / Test
        var isDevelopment = _environment == null || _environment.IsDevelopment();

        if (isDevelopment)
        {
            // 1. Check HTTP header from request (used by integration tests)
            var headerValue = _httpContextAccessor?.HttpContext?.Request.Headers[SimulationHeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue.Trim();
            }

            // 2. Fallback to programmatic SimulationAction if supplied in request model (used by unit tests)
            if (!string.IsNullOrWhiteSpace(request.SimulationAction))
            {
                return request.SimulationAction.Trim();
            }
        }

        // In Production (or normal Development flow without test headers), always default to Success
        return "Success";
    }

    private static string NormalizeActionToken(string action)
    {
        if (string.Equals(action, "Failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Declined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "FAIL_DECLINED", StringComparison.OrdinalIgnoreCase))
        {
            return "FAIL_DECLINED";
        }

        if (string.Equals(action, "InsufficientFunds", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "FAIL_FUNDS", StringComparison.OrdinalIgnoreCase))
        {
            return "FAIL_FUNDS";
        }

        return "SUCCESS";
    }

    private static string ComputeSignature(int orderId, int paymentId, decimal amount, string currency, string actionToken)
    {
        var raw = $"ORD:{orderId}:PAY:{paymentId}:AMT:{amount:F2}:CUR:{currency.ToUpperInvariant()}:ACT:{actionToken}:{ProviderSecretSalt}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..12];
    }

    private static bool TryParseAndVerifyReference(
        string reference,
        decimal expectedAmount,
        string expectedCurrency,
        out string action)
    {
        action = "Success";
        if (!TryParseReferenceParts(reference, out var orderId, out var paymentId, out var actionToken, out var signature))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(orderId, paymentId, expectedAmount, expectedCurrency, actionToken);
        if (!string.Equals(signature, expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        action = actionToken switch
        {
            "FAIL_DECLINED" => "Declined",
            "FAIL_FUNDS" => "InsufficientFunds",
            _ => "Success"
        };

        return true;
    }

    private static bool TryParseReferenceParts(
        string reference,
        out int orderId,
        out int paymentId,
        out string actionToken,
        out string signature)
    {
        orderId = 0;
        paymentId = 0;
        actionToken = string.Empty;
        signature = string.Empty;

        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("SIM-ORD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = reference.Split('-');
        if (parts.Length != 5)
        {
            return false;
        }

        if (!parts[1].StartsWith("ORD", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[1][3..], out orderId))
        {
            return false;
        }

        if (!parts[2].StartsWith("PAY", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2][3..], out paymentId))
        {
            return false;
        }

        actionToken = parts[3];
        signature = parts[4];
        return true;
    }
}
