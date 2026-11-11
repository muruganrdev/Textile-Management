using FluentValidation;
using SareeStore.Application.DTOs.Orders;

namespace SareeStore.Application.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ShippingAddressId)
            .GreaterThan(0)
            .WithMessage("A valid shipping address must be selected.");

        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("A valid payment method must be specified.");
    }
}
