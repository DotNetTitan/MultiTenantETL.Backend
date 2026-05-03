using FluentValidation;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using System.Text.Json;

namespace MultiTenantETL.Application.Connectors.Validators;

/// <summary>
/// Shared validation helpers for connector validators
/// </summary>
internal static class ConnectorValidationHelpers
{
    private static readonly string[] SupportedApiResponseFormats = MetadataConstants.ApiResponseFormats.Formats
        .Where(format => format.IsSupported)
        .Select(format => format.Value)
        .ToArray();

    public static readonly string ApiResponseFormatValidationMessage =
        $"API connector must have a valid responseFormat. Supported values: {string.Join(", ", SupportedApiResponseFormats)}";

    /// <summary>
    /// Validates API connector configuration, specifically the responseFormat field
    /// </summary>
    public static bool ValidateApiConfig(string type, string provider, JsonElement config)
    {
        // Only validate API connectors with REST provider
        if (type != ConnectorTypes.Api || provider != ConnectorProviders.REST)
        {
            return true;
        }

        try
        {
            // Check if responseFormat property exists and has a valid value
            if (config.TryGetProperty("responseFormat", out var responseFormatElement))
            {
                var responseFormat = responseFormatElement.GetString();
                if (string.IsNullOrWhiteSpace(responseFormat))
                {
                    return false;
                }

                // Validate against currently supported formats
                return SupportedApiResponseFormats.Contains(responseFormat, StringComparer.OrdinalIgnoreCase);
            }

            // responseFormat is missing
            return false;
        }
        catch
        {
            return false;
        }
    }
}

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
            .Must(BeValidType).WithMessage("Invalid connector type. Valid types are: Database, File, API, Email");

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
            .NotEmpty().WithMessage("Configuration is required")
            .Must((request, config) => ConnectorValidationHelpers.ValidateApiConfig(request.Type, request.Provider, config))
            .WithMessage(ConnectorValidationHelpers.ApiResponseFormatValidationMessage);
    }

    private static bool BeValidType(string type)
    {
        var validTypes = new[] { ConnectorTypes.Database, ConnectorTypes.File, ConnectorTypes.Api, ConnectorTypes.Email };
        return validTypes.Contains(type);
    }

    private static bool BeValidProviderForType(string type, string provider)
    {
        var validProviders = type switch
        {
            ConnectorTypes.Database => new[] { ConnectorProviders.SqlServer, ConnectorProviders.PostgreSQL, ConnectorProviders.MySQL },
            ConnectorTypes.File => new[] { ConnectorProviders.FTP, ConnectorProviders.SFTP, ConnectorProviders.AzureBlob },
            ConnectorTypes.Api => new[] { ConnectorProviders.REST },
            ConnectorTypes.Email => new[] { ConnectorProviders.Email },
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

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .MaximumLength(50).WithMessage("Type must not exceed 50 characters");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Provider is required")
            .MaximumLength(100).WithMessage("Provider must not exceed 100 characters");

        RuleFor(x => x.Direction)
            .NotEmpty().WithMessage("Direction is required")
            .MaximumLength(20).WithMessage("Direction must not exceed 20 characters")
            .Must(BeValidDirection).WithMessage("Invalid direction. Valid values are: source, destination, both");

        RuleFor(x => x.Config)
            .NotEmpty().WithMessage("Configuration is required")
            .Must((request, config) => ConnectorValidationHelpers.ValidateApiConfig(request.Type, request.Provider, config))
            .WithMessage(ConnectorValidationHelpers.ApiResponseFormatValidationMessage);
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
            .NotEmpty().WithMessage("Configuration is required")
            .Must((request, config) => ConnectorValidationHelpers.ValidateApiConfig(request.Type, request.Provider, config))
            .WithMessage(ConnectorValidationHelpers.ApiResponseFormatValidationMessage);
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
            .NotEmpty().WithMessage("Configuration is required")
            .Must((request, config) => ConnectorValidationHelpers.ValidateApiConfig(request.Type, request.Provider, config))
            .WithMessage(ConnectorValidationHelpers.ApiResponseFormatValidationMessage);

        RuleFor(x => x.TableOrResourceName)
            .MaximumLength(200).WithMessage("TableOrResourceName must not exceed 200 characters")
            .When(x => !string.IsNullOrEmpty(x.TableOrResourceName));
    }
}
