using SareeStore.Domain.Entities;

namespace SareeStore.Application.Common.Interfaces;

/// <summary>
/// Contract for generating JSON Web Tokens for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a signed JWT for the specified user containing their identity claims and role.
    /// </summary>
    /// <param name="user">The domain user entity.</param>
    /// <returns>A signed JWT string.</returns>
    string GenerateToken(User user);

    /// <summary>
    /// Calculates the UTC expiration timestamp for newly generated tokens.
    /// </summary>
    DateTime GetExpirationUtc();
}
