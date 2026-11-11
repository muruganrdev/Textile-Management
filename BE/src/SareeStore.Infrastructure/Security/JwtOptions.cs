using System.ComponentModel.DataAnnotations;

namespace SareeStore.Infrastructure.Security;

/// <summary>
/// Strongly-typed configuration options for JWT token generation and validation.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "JWT SecretKey is required.")]
    [MinLength(32, ErrorMessage = "JWT SecretKey must be at least 32 characters (256 bits) long.")]
    public string SecretKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "JWT Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "JWT Audience is required.")]
    public string Audience { get; set; } = string.Empty;

    [Range(1, 43200, ErrorMessage = "JWT ExpirationMinutes must be between 1 and 43200.")]
    public int ExpirationMinutes { get; set; } = 60;
}
