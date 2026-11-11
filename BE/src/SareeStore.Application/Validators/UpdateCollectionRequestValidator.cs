using FluentValidation;
using SareeStore.Application.DTOs.Collections;

namespace SareeStore.Application.Validators;

public class UpdateCollectionRequestValidator : AbstractValidator<UpdateCollectionRequest>
{
    public UpdateCollectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Collection name is required.")
            .MaximumLength(150).WithMessage("Collection name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.BannerImageUrl)
            .MaximumLength(500).WithMessage("Banner image URL cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.BannerImageUrl));
    }
}
