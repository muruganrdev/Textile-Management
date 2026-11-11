using FluentValidation.TestHelper;
using SareeStore.Application.DTOs.Products;
using SareeStore.Application.Validators;

namespace SareeStore.UnitTests.Validators;

public class ProductValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_PriceIsZeroOrNegative()
    {
        var model = new CreateProductRequest { Price = 0m };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Should_HaveError_When_DiscountPriceIsGreaterThanOrEqualToBasePrice()
    {
        var model = new CreateProductRequest
        {
            Price = 10000m,
            DiscountPrice = 12000m
        };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DiscountPrice);
    }

    [Fact]
    public void Should_HaveError_When_SareeLengthIsOutsideTraditionalRange()
    {
        var modelTooShort = new CreateProductRequest { SareeLengthMeters = 4.5m };
        var resultTooShort = _validator.TestValidate(modelTooShort);
        resultTooShort.ShouldHaveValidationErrorFor(x => x.SareeLengthMeters);

        var modelTooLong = new CreateProductRequest { SareeLengthMeters = 10.0m };
        var resultTooLong = _validator.TestValidate(modelTooLong);
        resultTooLong.ShouldHaveValidationErrorFor(x => x.SareeLengthMeters);
    }

    [Fact]
    public void Should_HaveError_When_SKUIsInvalidFormat()
    {
        var model = new CreateProductRequest { SKU = "INVALID SKU WITH SPACES!" };
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.SKU);
    }
}
