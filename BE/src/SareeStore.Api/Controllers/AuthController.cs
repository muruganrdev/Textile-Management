using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registers a new customer account. Public endpoint.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EndpointSummary("Register customer")]
    [EndpointDescription("Registers a new customer account, unconditionally assigning the Customer role and returning an access token.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authService.RegisterCustomerAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Authenticates user credentials and issues a JWT Bearer token. Public endpoint.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EndpointSummary("User login")]
    [EndpointDescription("Authenticates user email and password, returning an access token and user profile.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [EndpointSummary("Current user profile")]
    [EndpointDescription("Returns the safe profile of the currently authenticated user derived from validated claims.")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetCurrentUser(
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authenticated user identity could not be determined.",
                Instance = HttpContext.Request.Path
            });
        }

        var response = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(response);
    }
}
