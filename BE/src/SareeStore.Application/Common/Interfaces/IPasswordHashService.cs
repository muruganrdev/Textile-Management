using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

/// <summary>
/// Result of verifying a user's password against a stored hash.
/// </summary>
public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}

/// <summary>
/// Abstraction for secure password hashing and verification independent of external frameworks.
/// </summary>
public interface IPasswordHashService
{
    /// <summary>
    /// Hashes the plain-text password for the given user using a secure one-way salted algorithm.
    /// </summary>
    string HashPassword(User user, string password);

    /// <summary>
    /// Verifies whether the plain-text password matches the stored password hash.
    /// </summary>
    PasswordVerificationResult VerifyPassword(User user, string password);
}
