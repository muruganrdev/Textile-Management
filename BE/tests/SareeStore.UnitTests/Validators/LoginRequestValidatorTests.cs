using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Validators;

namespace SareeStore.UnitTests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        var request = new LoginRequest
        {
            Email = "customer@example.com",
            Password = "AnyPasswordProvided"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-an-email")]
    public async Task Validate_InvalidOrEmptyEmail_ShouldFail(string? email)
    {
        var request = new LoginRequest
        {
            Email = email!,
            Password = "Password123!"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptyPassword_ShouldFail(string? password)
    {
        var request = new LoginRequest
        {
            Email = "valid@example.com",
            Password = password!
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }
}
