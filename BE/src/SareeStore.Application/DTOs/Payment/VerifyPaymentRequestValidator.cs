using FluentValidation;

namespace SareeStore.Application.DTOs.Payment;

public class VerifyPaymentRequestValidator : AbstractValidator<VerifyPaymentRequest>
{
    public VerifyPaymentRequestValidator()
    {
        RuleFor(x => x.PaymentReference)
            .NotEmpty()
            .WithMessage("Payment reference is required.")
            .MaximumLength(100)
            .WithMessage("Payment reference must not exceed 100 characters.");
    }
}
