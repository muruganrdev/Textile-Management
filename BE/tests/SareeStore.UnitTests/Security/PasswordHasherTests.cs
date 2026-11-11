using Microsoft.AspNetCore.Identity;
using SareeStore.Domain.Entities;

namespace SareeStore.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly User _dummyUser = new()
    {
        Id = 1,
        FullName = "Test User",
        Email = "test@example.com"
    };

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyHashDifferentFromPlainText()
    {
        var rawPassword = "P@ssword123Secure!";

        var hash = _passwordHasher.HashPassword(_dummyUser, rawPassword);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.NotEqual(rawPassword, hash);
    }

    [Fact]
    public void VerifyHashedPassword_WithCorrectPassword_ShouldReturnSuccess()
    {
        var rawPassword = "StrongPassword#2026";
        var hash = _passwordHasher.HashPassword(_dummyUser, rawPassword);

        var result = _passwordHasher.VerifyHashedPassword(_dummyUser, hash, rawPassword);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WithWrongPassword_ShouldReturnFailed()
    {
        var rawPassword = "CorrectPassword123!";
        var wrongPassword = "WrongPassword123!";
        var hash = _passwordHasher.HashPassword(_dummyUser, rawPassword);

        var result = _passwordHasher.VerifyHashedPassword(_dummyUser, hash, wrongPassword);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void HashPassword_SamePasswordMultipleTimes_ShouldProduceUniqueSaltedHashes()
    {
        var password = "SaltUniquenessTestPassword!";

        var hash1 = _passwordHasher.HashPassword(_dummyUser, password);
        var hash2 = _passwordHasher.HashPassword(_dummyUser, password);

        Assert.NotEqual(hash1, hash2);
        Assert.Equal(PasswordVerificationResult.Success, _passwordHasher.VerifyHashedPassword(_dummyUser, hash1, password));
        Assert.Equal(PasswordVerificationResult.Success, _passwordHasher.VerifyHashedPassword(_dummyUser, hash2, password));
    }
}
