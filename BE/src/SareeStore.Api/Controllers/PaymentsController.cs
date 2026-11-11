using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Payment;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Customer")]
[Produces("application/json", "application/problem+json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// Verifies the outcome of an in-flight payment attempt.
    /// </summary>
    [HttpPost("{paymentId:int}/verify")]
    [EndpointSummary("Verify payment attempt")]
    [EndpointDescription("Verifies the payment result with the provider. Upon success, marks payment as Paid, confirms the order, and creates an audit status history record.")]
    [ProducesResponseType(typeof(PaymentVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentVerifyResponse>> VerifyPayment(
        int paymentId,
        [FromBody] VerifyPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var response = await _paymentService.VerifyPaymentAsync(customerId, paymentId, request, cancellationToken);
        return Ok(response);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Customer identifier could not be determined from authentication claims.");
        }

        return userId;
    }
}
