using SareeStore.Application.DTOs.Auth;
using SareeStore.Application.Validators;

namespace SareeStore.UnitTests.Validators;

public class CreateEmployeeRequestValidatorTests
{
    private readonly CreateEmployeeRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidRequest_ShouldPass()
    {
        var request = new CreateEmployeeRequest
        {
            FullName = "Kavitha Sundaram",
            Email = "kavitha.sundaram@sareestore.com",
            Password = "StaffPassword123#",
            PhoneNumber = "+919876543299"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_MissingFullName_ShouldFail(string? name)
    {
        var request = new CreateEmployeeRequest
        {
            FullName = name!,
            Email = "staff@sareestore.com",
            Password = "StaffPassword123#"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmployeeRequest.FullName));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("")]
    public async Task Validate_InvalidEmail_ShouldFail(string email)
    {
        var request = new CreateEmployeeRequest
        {
            FullName = "Staff Member",
            Email = email,
            Password = "StaffPassword123#"
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmployeeRequest.Email));
    }

    [Theory]
    [InlineData("weakpwd")]
    [InlineData("NoSpecialChar123")]
    public async Task Validate_WeakPassword_ShouldFail(string password)
    {
        var request = new CreateEmployeeRequest
        {
            FullName = "Staff Member",
            Email = "staff@sareestore.com",
            Password = password
        };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmployeeRequest.Password));
    }
}
