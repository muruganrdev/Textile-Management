using FluentValidation;
using SareeStore.Application.DTOs.Auth;

namespace SareeStore.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .Matches(@"^[^@\s]+@[^@\s\.]+\.[^@\s]+$").WithMessage("A valid email address is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
