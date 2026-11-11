using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Validators;

namespace SareeStore.UnitTests.Validators;

public class RegisterCustomerRequestValidatorTests
{
    private readonly RegisterCustomerRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        var request = new RegisterCustomerRequest
        {
            FullName = "Radha Raman",
            Email = "radha.raman@example.com",
            Password = "StrongPassword123#",
            PhoneNumber = "+919876543210"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Validate_MissingFullName_ShouldFail(string? fullName)
    {
        var request = new RegisterCustomerRequest
        {
            FullName = fullName!,
            Email = "valid@example.com",
            Password = "StrongPassword123#"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.FullName));
    }

    [Theory]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("username@.com")]
    public async Task Validate_InvalidEmail_ShouldFail(string email)
    {
        var request = new RegisterCustomerRequest
        {
            FullName = "Valid Name",
            Email = email,
            Password = "StrongPassword123#"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Email));
    }

    [Theory]
    [InlineData("short1!")] // < 8 characters
    [InlineData("nouppercase123!")] // no uppercase
    [InlineData("NOLOWERCASE123!")] // no lowercase
    [InlineData("NoDigitSpecial!")] // no digit
    [InlineData("NoSpecialChar123")] // no special character
    public async Task Validate_WeakPassword_ShouldFail(string password)
    {
        var request = new RegisterCustomerRequest
        {
            FullName = "Valid Name",
            Email = "valid@example.com",
            Password = password
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Password));
    }

    [Theory]
    [InlineData("abc-invalid-phone")]
    [InlineData("12")]
    public async Task Validate_InvalidPhoneNumber_ShouldFail(string phone)
    {
        var request = new RegisterCustomerRequest
        {
            FullName = "Valid Name",
            Email = "valid@example.com",
            Password = "StrongPassword123#",
            PhoneNumber = phone
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.PhoneNumber));
    }
}
