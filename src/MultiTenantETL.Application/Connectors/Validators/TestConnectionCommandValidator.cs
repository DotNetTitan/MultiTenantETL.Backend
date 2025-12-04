using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;

namespace MultiTenantETL.Application.Connectors.Validators;

public class TestConnectionCommandValidator : AbstractValidator<TestConnectionCommand>
{
    public TestConnectionCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
