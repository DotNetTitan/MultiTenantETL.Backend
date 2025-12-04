using FluentValidation;
using MultiTenantETL.Application.Pipelines.Models;

namespace MultiTenantETL.Application.Pipelines.Validators;

public class CreatePipelineRequestValidator : AbstractValidator<CreatePipelineRequest>
{
    public CreatePipelineRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.SourceConnectorId)
            .NotEmpty().WithMessage("Source connector is required");

        RuleFor(x => x.DestinationConnectorId)
            .NotEmpty().WithMessage("Destination connector is required");

        RuleFor(x => x.FieldMappings)
            .NotEmpty().WithMessage("Field mappings are required");
    }
}

public class UpdatePipelineRequestValidator : AbstractValidator<UpdatePipelineRequest>
{
    public UpdatePipelineRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.FieldMappings)
            .NotEmpty().WithMessage("Field mappings are required");
    }
}
