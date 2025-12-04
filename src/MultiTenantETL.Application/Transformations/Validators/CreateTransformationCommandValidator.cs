using FluentValidation;
using MultiTenantETL.Application.Transformations.Commands;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.Application.Transformations.Validators;

public class CreateTransformationCommandValidator : AbstractValidator<CreateTransformationCommand>
{
    public CreateTransformationCommandValidator()
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
            .Must(BeValidType).WithMessage("Invalid transformation type");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }

    private static bool BeValidType(string type)
    {
        var validTypes = new[] { 
            TransformationTypes.Filter, 
            TransformationTypes.Map, 
            TransformationTypes.String,
            TransformationTypes.Script,
            // Legacy types for backward compatibility
            TransformationTypes.FilterLegacy,
            TransformationTypes.MapLegacy,
            TransformationTypes.Trim,
            TransformationTypes.CaseConvert,
            TransformationTypes.Substring,
            TransformationTypes.Replace,
            TransformationTypes.ScriptLegacy
        };
        return validTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
    }
}
