using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;

namespace MultiTenantETL.Application.Connectors.Validators;

public class DetectSchemaCommandValidator : AbstractValidator<DetectSchemaCommand>
{
    public DetectSchemaCommandValidator()
    {
        RuleFor(x => x.ConnectorId)
            .NotEmpty().WithMessage("ConnectorId is required");

        RuleFor(x => x.TableOrResourceName)
            .MaximumLength(200).WithMessage("TableOrResourceName must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TableOrResourceName));

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
