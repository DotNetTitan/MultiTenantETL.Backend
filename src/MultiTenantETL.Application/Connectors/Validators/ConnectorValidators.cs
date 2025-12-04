using FluentValidation;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;

namespace MultiTenantETL.Application.Connectors.Validators;

public class CreateConnectorRequestValidator : AbstractValidator<CreateConnectorRequest>
{
    public CreateConnectorRequestValidator()
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
            .Must(BeValidType).WithMessage("Invalid connector type. Valid types are: Database, File, API");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters")
            .Must((request, provider) => BeValidProviderForType(request.Type, provider))
            .WithMessage("Invalid provider for the specified type");

        RuleFor(x => x.Direction)
            .NotEmpty().WithMessage("Direction is required")
            .MaximumLength(20).WithMessage("Direction must not exceed 20 characters")
            .Must(BeValidDirection).WithMessage("Invalid direction. Valid values are: source, destination, both");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");
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

public class UpdateConnectorRequestValidator : AbstractValidator<UpdateConnectorRequest>
{
    public UpdateConnectorRequestValidator()
    {
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
    }

    private static bool BeValidDirection(string direction)
    {
        var validDirections = new[] { ConnectorDirections.Source, ConnectorDirections.Destination, ConnectorDirections.Both };
        return validDirections.Contains(direction.ToLower());
    }
}

public class TestConnectionRequestValidator : AbstractValidator<TestConnectionRequest>
{
    public TestConnectionRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");
    }
}

public class DetectSchemaRequestValidator : AbstractValidator<DetectSchemaRequest>
{
    public DetectSchemaRequestValidator()
    {
        RuleFor(x => x.ConnectorId)
            .NotEmpty().WithMessage("ConnectorId is required");

        RuleFor(x => x.TableOrResourceName)
            .MaximumLength(200).WithMessage("TableOrResourceName must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TableOrResourceName));
    }
}

public class DetectSchemaPreviewRequestValidator : AbstractValidator<DetectSchemaPreviewRequest>
{
    public DetectSchemaPreviewRequestValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.TableOrResourceName)
            .MaximumLength(200).WithMessage("TableOrResourceName must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TableOrResourceName));
    }
}
