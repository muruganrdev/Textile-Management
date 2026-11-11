using FluentValidation;
using SareeStore.Domain.Enums;

namespace SareeStore.Application.DTOs.Payment;

public class InitiatePaymentRequestValidator : AbstractValidator<InitiatePaymentRequest>
{
    public InitiatePaymentRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .IsInEnum()
            .WithMessage("A valid payment method is required.")
            .Must(method => method != PaymentMethod.COD)
            .WithMessage("Cash on Delivery orders cannot be processed through online payment initiation.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(100)
            .WithMessage("Idempotency key must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.IdempotencyKey));
    }
}
