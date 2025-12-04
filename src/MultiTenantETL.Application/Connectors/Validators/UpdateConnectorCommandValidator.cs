using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.Application.Connectors.Validators;

public class UpdateConnectorCommandValidator : AbstractValidator<UpdateConnectorCommand>
{
    public UpdateConnectorCommandValidator()
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

        RuleFor(x => x.Direction)
            .NotEmpty().WithMessage("Direction is required")
            .MaximumLength(20).WithMessage("Direction must not exceed 20 characters")
            .Must(BeValidDirection).WithMessage("Invalid direction. Valid values are: source, destination, both");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }

    private static bool BeValidDirection(string direction)
    {
        var validDirections = new[] { ConnectorDirections.Source, ConnectorDirections.Destination, ConnectorDirections.Both };
        return validDirections.Contains(direction.ToLower());
    }
}
