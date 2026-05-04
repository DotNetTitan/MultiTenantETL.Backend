using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting;

public class ConnectionTester : IConnectionTester
{
    private readonly IDatabaseConnectionTester _databaseTester;
    private readonly IStorageConnectionTester _storageTester;
    private readonly IApiConnectionTester _apiTester;
    private readonly ILogger<ConnectionTester> _logger;

    public ConnectionTester(
        IDatabaseConnectionTester databaseTester,
        IStorageConnectionTester storageTester,
        IApiConnectionTester apiTester,
        ILogger<ConnectionTester> logger)
    {
        _databaseTester = databaseTester;
        _storageTester = storageTester;
        _apiTester = apiTester;
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string type, string provider, JsonElement config)
    {
        try
        {
            return type switch
            {
                ConnectorTypes.Database => await _databaseTester.TestConnectionAsync(provider, config),
                ConnectorTypes.File => await _storageTester.TestConnectionAsync(provider, config),
                ConnectorTypes.Api => await _apiTester.TestConnectionAsync(provider, config),
                ConnectorTypes.Email => ValidateEmailConfig(config),
                _ => new ConnectionTestResult
                {
                    Success = false,
                    Message = $"Unsupported connector type: {type}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for type {Type}, provider {Provider}", type, provider);
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates email connector configuration by checking that required fields are present.
    /// </summary>
    private static ConnectionTestResult ValidateEmailConfig(JsonElement config)
    {
        var hasRecipients = config.TryGetProperty("recipients", out var recipients)
                            && recipients.ValueKind == JsonValueKind.Array
                            && recipients.GetArrayLength() > 0;

        var hasSubject = config.TryGetProperty("subject", out var subject)
                         && subject.ValueKind == JsonValueKind.String
                         && !string.IsNullOrWhiteSpace(subject.GetString());

        var hasFormat = config.TryGetProperty("attachmentFormat", out var format)
                        && format.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(format.GetString());

        if (!hasRecipients)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "At least one recipient email address is required"
            };
        }

        if (!hasSubject)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Email subject is required"
            };
        }

        if (!hasFormat)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = "Attachment format is required (CSV, JSON, or Excel)"
            };
        }

        return new ConnectionTestResult
        {
            Success = true,
            Message = "Email configuration is valid"
        };
    }
}
