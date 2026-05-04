using MultiTenantETL.Application.Connectors;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Api;

public interface IApiConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
