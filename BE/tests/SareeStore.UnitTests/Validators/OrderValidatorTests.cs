using FluentValidation.TestHelper;
using SareeStore.Application.DTOs.Orders;
using SareeStore.Application.Validators;
using SareeStore.Domain.Enums;

namespace SareeStore.UnitTests.Validators;

public class OrderValidatorTests
{
    private readonly CreateOrderRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValidRequest_ShouldNotHaveAnyErrors()
    {
        var request = new CreateOrderRequest
        {
            ShippingAddressId = 1,
            PaymentMethod = PaymentMethod.COD
        };

        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenShippingAddressIdInvalid_ShouldHaveError(int invalidAddressId)
    {
        var request = new CreateOrderRequest
        {
            ShippingAddressId = invalidAddressId,
            PaymentMethod = PaymentMethod.UPI
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ShippingAddressId);
    }

    [Fact]
    public void Validate_WhenInvalidPaymentMethod_ShouldHaveError()
    {
        var request = new CreateOrderRequest
        {
            ShippingAddressId = 1,
            PaymentMethod = (PaymentMethod)999
        };

        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }
}
