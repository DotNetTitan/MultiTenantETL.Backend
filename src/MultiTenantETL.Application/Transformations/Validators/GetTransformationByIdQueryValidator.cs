using FluentValidation;
using MultiTenantETL.Application.Transformations.Queries;

namespace MultiTenantETL.Application.Transformations.Validators;

public class GetTransformationByIdQueryValidator : AbstractValidator<GetTransformationByIdQuery>
{
    public GetTransformationByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
