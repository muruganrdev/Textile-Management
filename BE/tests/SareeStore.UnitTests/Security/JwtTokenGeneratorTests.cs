using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SareeStore.Domain.Entities;
using SareeStore.Domain.Enums;
using SareeStore.Infrastructure.Security;

namespace SareeStore.UnitTests.Security;

public class JwtTokenGeneratorTests
{
    private readonly JwtOptions _validOptions = new()
    {
        SecretKey = "SUPER_SECURE_TEST_SECRET_KEY_FOR_UNIT_TESTS_1234567890",
        Issuer = "SareeStore.TestIssuer",
        Audience = "SareeStore.TestAudience",
        ExpirationMinutes = 60
    };

    [Fact]
    public void GenerateToken_WithValidCustomerUser_ShouldGenerateValidSignedTokenWithExpectedClaims()
    {
        // Arrange
        var optionsMock = Options.Create(_validOptions);
        var generator = new JwtTokenGenerator(optionsMock);

        var user = new User
        {
            Id = 101,
            FullName = "Priya Sharma",
            Email = "priya.sharma@example.com",
            Role = UserRole.Customer,
            IsActive = true
        };

        // Act
        var tokenString = generator.GenerateToken(user);

        // Assert
        Assert.NotNull(tokenString);
        Assert.NotEmpty(tokenString);

        // Validate token signature, issuer, audience, and claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _validOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _validOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_validOptions.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);
        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);

        // Verify claims
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
        var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;

        Assert.Equal("101", idClaim);
        Assert.Equal("priya.sharma@example.com", emailClaim);
        Assert.Equal("Priya Sharma", nameClaim);
        Assert.Equal(nameof(UserRole.Customer), roleClaim);

        // Verify expiration
        var jwtToken = (JwtSecurityToken)validatedToken;
        Assert.True(jwtToken.ValidTo > DateTime.UtcNow);
        Assert.True(jwtToken.ValidTo <= DateTime.UtcNow.AddMinutes(61));

        // Verify Jti unique ID
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        Assert.NotNull(jti);
        Assert.NotEmpty(jti);
    }

    [Theory]
    [InlineData(UserRole.SuperAdmin)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Customer)]
    public void GenerateToken_WithDifferentRoles_ShouldIncludeMatchingRoleClaim(UserRole role)
    {
        // Arrange
        var optionsMock = Options.Create(_validOptions);
        var generator = new JwtTokenGenerator(optionsMock);

        var user = new User
        {
            Id = 202,
            FullName = "Test Staff",
            Email = "staff@example.com",
            Role = role,
            IsActive = true
        };

        // Act
        var tokenString = generator.GenerateToken(user);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(tokenString);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        Assert.Equal(role.ToString(), roleClaim);
    }

    [Fact]
    public void GenerateToken_NullUser_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optionsMock = Options.Create(_validOptions);
        var generator = new JwtTokenGenerator(optionsMock);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => generator.GenerateToken(null!));
    }
}
