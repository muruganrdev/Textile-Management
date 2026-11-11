namespace SareeStore.Application.DTOs.Auth;

/// <summary>
/// Authentication response containing JWT token, expiration, and sanitized user profile.
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public UserResponse User { get; set; } = null!;
}
