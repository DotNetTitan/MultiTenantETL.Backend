using FluentValidation;
using MultiTenantETL.Application.Pipelines.Commands;

namespace MultiTenantETL.Application.Pipelines.Validators;

public class ExecutePipelineCommandValidator : AbstractValidator<ExecutePipelineCommand>
{
    public ExecutePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}
