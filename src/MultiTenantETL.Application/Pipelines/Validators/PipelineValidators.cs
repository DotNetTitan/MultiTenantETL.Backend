using FluentValidation;
using MultiTenantETL.Application.Pipelines.Commands;

namespace MultiTenantETL.Application.Pipelines.Validators;

public class CreatePipelineCommandValidator : AbstractValidator<CreatePipelineCommand>
{
    public CreatePipelineCommandValidator()
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

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}

public class UpdatePipelineCommandValidator : AbstractValidator<UpdatePipelineCommand>
{
    public UpdatePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.FieldMappings)
            .NotEmpty().WithMessage("Field mappings are required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}

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

public class TogglePipelineStatusCommandValidator : AbstractValidator<TogglePipelineStatusCommand>
{
    public TogglePipelineStatusCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}

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

public class GetPipelineByIdQueryValidator : AbstractValidator<Queries.GetPipelineByIdQuery>
{
    public GetPipelineByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");
    }
}
