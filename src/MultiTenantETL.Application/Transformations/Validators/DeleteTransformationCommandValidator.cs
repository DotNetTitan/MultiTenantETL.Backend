using FluentValidation;
using MultiTenantETL.Application.Transformations.Commands;

namespace MultiTenantETL.Application.Transformations.Validators;

public class DeleteTransformationCommandValidator : AbstractValidator<DeleteTransformationCommand>
{
    public DeleteTransformationCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
