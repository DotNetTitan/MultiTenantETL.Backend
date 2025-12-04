using FluentValidation;
using MultiTenantETL.Application.Connectors.Commands;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.Application.Connectors.Validators;

public class CreateConnectorCommandValidator : AbstractValidator<CreateConnectorCommand>
{
    public CreateConnectorCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(2).WithMessage("Name must be at least 2 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters")
            .Must(BeValidType).WithMessage("Invalid connector type. Valid types are: Database, File, Api");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters")
            .Must((command, provider) => BeValidProviderForType(command.Type, provider))
            .WithMessage("Invalid provider for the specified type");

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

    private static bool BeValidType(string type)
    {
        var validTypes = new[] { ConnectorTypes.Database, ConnectorTypes.File, ConnectorTypes.Api };
        return validTypes.Contains(type);
    }

    private static bool BeValidProviderForType(string type, string provider)
    {
        var validProviders = type switch
        {
            ConnectorTypes.Database => new[] { ConnectorProviders.SqlServer, ConnectorProviders.PostgreSQL, ConnectorProviders.MySQL },
            ConnectorTypes.File => new[] { ConnectorProviders.Local, ConnectorProviders.FTP, ConnectorProviders.SFTP, ConnectorProviders.S3, ConnectorProviders.AzureBlob },
            ConnectorTypes.Api => new[] { ConnectorProviders.REST },
            _ => Array.Empty<string>()
        };

        return validProviders.Contains(provider);
    }

    private static bool BeValidDirection(string direction)
    {
        var validDirections = new[] { ConnectorDirections.Source, ConnectorDirections.Destination, ConnectorDirections.Both };
        return validDirections.Contains(direction.ToLower());
    }
}
