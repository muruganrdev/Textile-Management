using Microsoft.AspNetCore.Identity;
using SareeStore.Application.Common.Interfaces;
using SareeStore.Domain.Entities;
using AppPasswordVerificationResult = SareeStore.Application.Common.Interfaces.PasswordVerificationResult;
using IdentityPasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace SareeStore.Infrastructure.Security;

/// <summary>
/// Infrastructure adapter implementing Application IPasswordHashService using ASP.NET Core Identity's PasswordHasher.
/// Shields Application layer from Microsoft Identity dependencies.
/// </summary>
public class PasswordHashService : IPasswordHashService
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public PasswordHashService(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _passwordHasher.HashPassword(user, password);
    }

    public AppPasswordVerificationResult VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(password))
        {
            return AppPasswordVerificationResult.Failed;
        }

        var identityResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return identityResult switch
        {
            IdentityPasswordVerificationResult.Success => AppPasswordVerificationResult.Success,
            IdentityPasswordVerificationResult.SuccessRehashNeeded => AppPasswordVerificationResult.SuccessRehashNeeded,
            _ => AppPasswordVerificationResult.Failed
        };
    }
}
