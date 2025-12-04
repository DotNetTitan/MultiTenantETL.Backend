using FluentValidation;
using MultiTenantETL.Application.Pipelines.Queries;

namespace MultiTenantETL.Application.Pipelines.Validators;

public class GetPipelineByIdQueryValidator : AbstractValidator<GetPipelineByIdQuery>
{
    public GetPipelineByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
