using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;
using MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

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
}
