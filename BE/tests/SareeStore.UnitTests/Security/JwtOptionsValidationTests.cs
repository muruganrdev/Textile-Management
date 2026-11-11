using System.ComponentModel.DataAnnotations;
using SareeStore.Infrastructure.Security;

namespace SareeStore.UnitTests.Security;

public class JwtOptionsValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void JwtOptions_ValidConfiguration_ShouldHaveNoValidationErrors()
    {
        var options = new JwtOptions
        {
            SecretKey = "VALID_SECRET_KEY_WITH_SUFFICIENT_ENTROPY_AND_LENGTH_123456",
            Issuer = "SareeStore.Api",
            Audience = "SareeStore.Client",
            ExpirationMinutes = 120
        };

        var errors = ValidateModel(options);
        Assert.Empty(errors);
    }

    [Fact]
    public void JwtOptions_SecretKeyShorterThan32Characters_ShouldFailValidation()
    {
        var options = new JwtOptions
        {
            SecretKey = "too-short-secret", // 16 chars < 32
            Issuer = "SareeStore.Api",
            Audience = "SareeStore.Client",
            ExpirationMinutes = 60
        };

        var errors = ValidateModel(options);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JwtOptions.SecretKey)));
    }

    [Fact]
    public void JwtOptions_MissingSecretKey_ShouldFailValidation()
    {
        var options = new JwtOptions
        {
            SecretKey = "",
            Issuer = "SareeStore.Api",
            Audience = "SareeStore.Client",
            ExpirationMinutes = 60
        };

        var errors = ValidateModel(options);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JwtOptions.SecretKey)));
    }

    [Fact]
    public void JwtOptions_MissingIssuerOrAudience_ShouldFailValidation()
    {
        var options = new JwtOptions
        {
            SecretKey = "VALID_SECRET_KEY_WITH_SUFFICIENT_ENTROPY_AND_LENGTH_123456",
            Issuer = "",
            Audience = "",
            ExpirationMinutes = 60
        };

        var errors = ValidateModel(options);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JwtOptions.Issuer)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JwtOptions.Audience)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(50000)]
    public void JwtOptions_InvalidExpirationMinutes_ShouldFailValidation(int expiration)
    {
        var options = new JwtOptions
        {
            SecretKey = "VALID_SECRET_KEY_WITH_SUFFICIENT_ENTROPY_AND_LENGTH_123456",
            Issuer = "SareeStore.Api",
            Audience = "SareeStore.Client",
            ExpirationMinutes = expiration
        };

        var errors = ValidateModel(options);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JwtOptions.ExpirationMinutes)));
    }
}
