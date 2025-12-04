using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;

namespace MultiTenantETL.Application.Connectors.Validators;

public class DeleteConnectorCommandValidator : AbstractValidator<DeleteConnectorCommand>
{
    public DeleteConnectorCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
