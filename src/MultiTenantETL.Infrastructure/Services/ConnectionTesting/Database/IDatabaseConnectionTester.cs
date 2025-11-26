using System.Text.Json;
using MultiTenantETL.Application.Connectors;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;

public interface IDatabaseConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
