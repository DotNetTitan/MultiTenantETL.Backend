using MultiTenantETL.Application.Connectors;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Services.ConnectionTesting.Storage;

public interface IStorageConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string provider, JsonElement config);
}
