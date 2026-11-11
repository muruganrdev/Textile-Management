using FluentValidation;
using SareeStore.Application.DTOs.Cart;

namespace SareeStore.Application.Validators;

public class AddToCartRequestValidator : AbstractValidator<AddToCartRequest>
{
    public AddToCartRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0)
            .WithMessage("A valid Product ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Quantity must be at least 1.");
    }
}
