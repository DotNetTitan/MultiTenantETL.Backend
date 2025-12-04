using System.Text.Json;

namespace MultiTenantETL.Application.Connectors.Commands;

/// <summary>
/// Command to detect schema for an existing connector
/// </summary>
public record DetectSchemaCommand(
    Guid ConnectorId,
    string? TableOrResourceName,
    Guid TenantId);

/// <summary>
/// Command to preview schema detection for a new connector configuration
/// </summary>
public record DetectSchemaPreviewCommand(
    string Type,
    string Provider,
    JsonElement Config,
    string? TableOrResourceName,
    Guid TenantId);
