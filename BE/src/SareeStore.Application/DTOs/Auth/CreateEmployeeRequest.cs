namespace SareeStore.Application.DTOs.Auth;

/// <summary>
/// Administrative request DTO for creating an employee account.
/// Allowed only for authorized SuperAdmin users.
/// </summary>
public class CreateEmployeeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
