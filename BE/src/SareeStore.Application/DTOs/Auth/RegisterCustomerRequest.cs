namespace SareeStore.Application.DTOs.Auth;

/// <summary>
/// Public request DTO for registering a new customer account.
/// Note: Role is intentionally excluded to prevent privilege escalation.
/// </summary>
public class RegisterCustomerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
