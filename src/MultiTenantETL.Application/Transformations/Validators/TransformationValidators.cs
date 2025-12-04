using FluentValidation;
using MultiTenantETL.Application.Transformations.Models;

namespace MultiTenantETL.Application.Transformations.Validators;

public class CreateTransformationRequestValidator : AbstractValidator<CreateTransformationRequest>
{
    private static readonly string[] ValidTransformationTypes = new[]
    {
        "Filter", "Map", "Trim", "Case Convert", "Substring", "Replace", "Script"
    };

    public CreateTransformationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters")
            .Must(BeValidType).WithMessage($"Invalid transformation type. Valid types are: {string.Join(", ", ValidTransformationTypes)}");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");
    }

    private static bool BeValidType(string type)
    {
        return ValidTransformationTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    }
}

public class UpdateTransformationRequestValidator : AbstractValidator<UpdateTransformationRequest>
{
    public UpdateTransformationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");
    }
}
