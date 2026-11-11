namespace SareeStore.Application.DTOs.Auth;

/// <summary>
/// Public request DTO for authenticating an existing user.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
