using FluentValidation;
using SareeStore.Application.DTOs.Products;

namespace SareeStore.Application.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Saree title is required.")
            .MaximumLength(250).WithMessage("Title cannot exceed 250 characters.");

        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU code is required.")
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters.")
            .Matches(@"^[A-Za-z0-9-]+$").WithMessage("SKU must only contain alphanumeric characters and hyphens.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("A valid Category ID is required.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero INR.");

        RuleFor(x => x.DiscountPrice)
            .GreaterThan(0).WithMessage("Discount price must be greater than zero.")
            .LessThan(x => x.Price).WithMessage("Discount price must be less than the base price.")
            .When(x => x.DiscountPrice.HasValue);

        RuleFor(x => x.Fabric)
            .NotEmpty().WithMessage("Fabric is required.")
            .MaximumLength(100).WithMessage("Fabric name cannot exceed 100 characters.");

        RuleFor(x => x.WeaveType)
            .NotEmpty().WithMessage("Weave type is required.")
            .MaximumLength(100).WithMessage("Weave type cannot exceed 100 characters.");

        RuleFor(x => x.ZariType)
            .NotEmpty().WithMessage("Zari type is required.")
            .MaximumLength(100).WithMessage("Zari type cannot exceed 100 characters.");

        RuleFor(x => x.PrimaryColor)
            .NotEmpty().WithMessage("Primary color is required.")
            .MaximumLength(50).WithMessage("Primary color cannot exceed 50 characters.");

        RuleFor(x => x.SareeLengthMeters)
            .InclusiveBetween(5.0m, 9.5m).WithMessage("Traditional saree length must be between 5.0 and 9.5 meters.");

        RuleFor(x => x.SareeWidthInches)
            .InclusiveBetween(40.0m, 54.0m).WithMessage("Saree width must be between 40.0 and 54.0 inches.");

        RuleFor(x => x.BlouseLengthMeters)
            .InclusiveBetween(0.5m, 2.0m).WithMessage("Blouse length must be between 0.5 and 2.0 meters.")
            .When(x => x.BlouseLengthMeters.HasValue);

        RuleFor(x => x.CraftDetails)
            .MaximumLength(500).WithMessage("Craft details cannot exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.CraftDetails));

        RuleFor(x => x.Occasion)
            .MaximumLength(100).WithMessage("Occasion cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Occasion));
    }
}
