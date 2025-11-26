using System.Text.Json;
using MultiTenantETL.Application.Connectors;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public interface IStorageConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
