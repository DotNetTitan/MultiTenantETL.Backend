using FluentValidation;
using MultiTenantETL.Application.Pipelines.Commands;

namespace MultiTenantETL.Application.Pipelines.Validators;

public class DeletePipelineCommandValidator : AbstractValidator<DeletePipelineCommand>
{
    public DeletePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
