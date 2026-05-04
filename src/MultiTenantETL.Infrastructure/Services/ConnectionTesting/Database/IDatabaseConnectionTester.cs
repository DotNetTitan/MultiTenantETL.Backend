using MultiTenantETL.Application.Connectors;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Database;

public interface IDatabaseConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
