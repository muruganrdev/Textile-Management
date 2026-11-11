using FluentValidation;
using SareeStore.Application.DTOs.Cart;

namespace SareeStore.Application.Validators;

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Quantity must be at least 1.");
    }
}
