using SareeStore.Application.DTOs.Auth;

namespace SareeStore.Application.Services.Interfaces;

/// <summary>
/// Application service contract for user authentication, registration, and employee onboarding.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new customer account, assigns Customer role, and issues a JWT token.
    /// </summary>
    Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates user credentials, rejects deactivated accounts, handles rehashing, and issues a JWT token.
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a new employee account with Employee role. Callable only by an authorized SuperAdmin.
    /// </summary>
    Task<UserResponse> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user profile by validated user identifier. Throws UnauthorizedException if user is inactive or not found.
    /// </summary>
    Task<UserResponse> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}
