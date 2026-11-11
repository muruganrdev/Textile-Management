using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Services.Interfaces;

namespace SareeStore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Creates a new employee account. Restricted exclusively to SuperAdmin.
    /// </summary>
    [HttpPost("employees")]
    [Authorize(Roles = "SuperAdmin")]
    [EndpointSummary("Create employee account")]
    [EndpointDescription("Provisions a new staff employee account. Callable exclusively by SuperAdmin.")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authService.CreateEmployeeAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
