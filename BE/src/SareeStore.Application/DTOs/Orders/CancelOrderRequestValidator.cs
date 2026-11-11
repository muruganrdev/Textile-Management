using FluentValidation;

namespace SareeStore.Application.DTOs.Orders;

public class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(500).WithMessage("Cancellation reason cannot exceed 500 characters.");
    }
}
