using System.Text.Json;

namespace MultiTenantETL.Application.Connectors;

public interface ISchemaDetector
{
    Task<SchemaDetectionResult> DetectSchemaAsync(string type, string provider, JsonElement config, string? tableOrResourceName);
}

public record SchemaDetectionResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public JsonElement? Schema { get; init; }
}
