using Microsoft.AspNetCore.Identity;
using NSubstitute;
using SareeStore.Domain.Entities;
using SareeStore.Infrastructure.Security;
using AppPasswordVerificationResult = SareeStore.Application.Common.Interfaces.PasswordVerificationResult;
using IdentityPasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace SareeStore.UnitTests.Security;

public class PasswordHashServiceTests
{
    private readonly IPasswordHasher<User> _identityHasherMock = Substitute.For<IPasswordHasher<User>>();
    private readonly PasswordHashService _service;
    private readonly User _user = new()
    {
        Id = 1,
        FullName = "Test User",
        Email = "test@example.com",
        PasswordHash = "existing_stored_hash"
    };

    public PasswordHashServiceTests()
    {
        _service = new PasswordHashService(_identityHasherMock);
    }

    [Fact]
    public void HashPassword_ShouldCallIdentityHasherAndReturnHash()
    {
        _identityHasherMock.HashPassword(_user, "PlainPassword123!")
            .Returns("identity_generated_hash");

        var hash = _service.HashPassword(_user, "PlainPassword123!");

        Assert.Equal("identity_generated_hash", hash);
    }

    [Theory]
    [InlineData(IdentityPasswordVerificationResult.Success, AppPasswordVerificationResult.Success)]
    [InlineData(IdentityPasswordVerificationResult.SuccessRehashNeeded, AppPasswordVerificationResult.SuccessRehashNeeded)]
    [InlineData(IdentityPasswordVerificationResult.Failed, AppPasswordVerificationResult.Failed)]
    public void VerifyPassword_ShouldMapIdentityVerificationResultToApplicationEnum(
        IdentityPasswordVerificationResult identityResult,
        AppPasswordVerificationResult expectedAppResult)
    {
        _identityHasherMock.VerifyHashedPassword(_user, _user.PasswordHash, "SomePassword")
            .Returns(identityResult);

        var result = _service.VerifyPassword(_user, "SomePassword");

        Assert.Equal(expectedAppResult, result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ShouldReturnFailedWithoutCallingIdentity()
    {
        var result = _service.VerifyPassword(_user, "");

        Assert.Equal(AppPasswordVerificationResult.Failed, result);
        _identityHasherMock.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
