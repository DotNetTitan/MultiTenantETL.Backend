using System.Text.Json;
using MultiTenantETL.Application.Connectors;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api;

public interface IApiConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
