using System.Text.Json;

namespace MultiTenantETL.Application.Connectors;

public interface IConnectionTester
{
    Task<ConnectionTestResult> TestConnectionAsync(string type, string provider, JsonElement config);
}

public record ConnectionTestResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}
