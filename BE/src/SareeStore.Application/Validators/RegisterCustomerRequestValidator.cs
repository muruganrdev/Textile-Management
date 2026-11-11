using FluentValidation;
using SareeStore.Application.DTOs.Auth;

namespace SareeStore.Application.Validators;

public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .Matches(@"^[^@\s]+@[^@\s\.]+\.[^@\s]+$").WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters.")
            .Matches(@"^\+?[0-9\s-]{7,20}$").WithMessage("Phone number format is invalid.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
