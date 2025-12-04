using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;

namespace MultiTenantETL.Application.Connectors.Validators;

public class DetectSchemaPreviewCommandValidator : AbstractValidator<DetectSchemaPreviewCommand>
{
    public DetectSchemaPreviewCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.TableOrResourceName)
            .MaximumLength(200).WithMessage("TableOrResourceName must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TableOrResourceName));

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
