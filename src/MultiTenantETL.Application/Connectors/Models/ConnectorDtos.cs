using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MultiTenantETL.Application.Connectors.Models;

public record CreateConnectorRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Type { get; init; } // Database, File, API

    public required string Provider { get; init; } // SqlServer, PostgreSQL, CSV, etc.

    public required string Direction { get; init; } // source, destination, both

    public required JsonElement Config { get; init; } // Type-specific configuration

    public JsonElement? Schema { get; init; } // Optional schema definition
}

public record UpdateConnectorRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Type { get; init; } // Read-only, used for validation only

    public required string Provider { get; init; } // Read-only, used for validation only

    public required string Direction { get; init; }

    public required JsonElement Config { get; init; }

    public JsonElement? Schema { get; init; }

    public bool? IsActive { get; init; }
}

public record ConnectorResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public required string Provider { get; init; }
    public required string Direction { get; init; }
    public bool IsSource { get; init; }
    public bool IsDestination { get; init; }
    public bool RequiresCredentials { get; init; }
    public bool IsActive { get; init; }
    public required JsonElement Config { get; init; }
    public JsonElement? Schema { get; init; }
    public DateTime? LastTestedAt { get; init; }
    public string? LastTestResult { get; init; }
    public string? LastTestMessage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record ConnectorListResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
    public required string Provider { get; init; }
    public required string Direction { get; init; }
    public bool IsSource { get; init; }
    public bool IsDestination { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastTestedAt { get; init; }
    public string? LastTestResult { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record TestConnectionRequest
{
    public required string Type { get; init; }

    public required string Provider { get; init; }

    public required JsonElement Config { get; init; }
}

public record TestConnectionResponse
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public DateTime TestedAt { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}

public record DetectSchemaRequest
{
    public Guid ConnectorId { get; init; }

    public string? TableOrResourceName { get; init; } // For databases: table name, For APIs: endpoint
}

public record DetectSchemaPreviewRequest
{
    public required string Type { get; init; } // Database, File, API

    public required string Provider { get; init; } // SqlServer, PostgreSQL, MySQL, etc.

    public required JsonElement Config { get; init; } // Connection configuration

    public string? TableOrResourceName { get; init; } // For databases: table name
}

public record DetectSchemaResponse
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public JsonElement? Schema { get; init; }
    public DateTime DetectedAt { get; init; }
}

public record SchemaField
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string? DefaultValue { get; init; }
}

public record ConnectorSearchRequest
{
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Provider { get; init; }
    public string? Direction { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record PagedConnectorResponse
{
    public List<ConnectorListResponse> Connectors { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

// Configuration models for different connector types
public record DatabaseConfig
{
    public string? Host { get; init; }
    public int Port { get; init; }
    public string? Database { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool UseSsl { get; init; }
    public bool UseCustomConnectionString { get; init; }
    public string? ConnectionString { get; init; }
    public Dictionary<string, string>? AdditionalParameters { get; init; }
    // Cosmos DB fields
    public string? CosmosEndpoint { get; init; }
    public string? CosmosKey { get; init; }
    public string? Container { get; init; }
    public string? Query { get; init; }
}

public record FileConfig
{
    public string? Path { get; init; }
    public string? Format { get; init; } // CSV, Excel, JSON
    public string? Delimiter { get; init; } // For CSV
    public bool HasHeader { get; init; } // For CSV/Excel
    public string? SheetName { get; init; } // For Excel
    public string? Encoding { get; init; }
    // Storage provider specific fields
    // FTP
    public string? FtpHost { get; init; }
    public int? FtpPort { get; init; }
    public string? FtpUsername { get; init; }
    public string? FtpPassword { get; init; }
    // SFTP
    public string? SftpHost { get; init; }
    public int? SftpPort { get; init; }
    public string? SftpUsername { get; init; }
    public string? SftpPassword { get; init; }
    // Azure Blob
    public string? AzureAccountName { get; init; }
    public string? AzureContainer { get; init; }
    public string? AzureAccountKey { get; init; }
    public Dictionary<string, string>? AdditionalParameters { get; init; }
}

public record ApiConfig
{
    public string? BaseUrl { get; init; }
    public string? AuthType { get; init; } // None, Basic, Bearer, ApiKey
    public string? AuthToken { get; init; }
    public string? ApiKeyHeader { get; init; } // Header name for API key (e.g., X-API-Key, Authorization)
    public string? ApiKeyValue { get; init; } // API key value
    public string? Username { get; init; }
    public string? Password { get; init; }
    
    // Dynamic token generation (for Bearer auth)
    public bool UseDynamicToken { get; init; }
    public string? TokenEndpointUrl { get; init; }
    public string? TokenEndpointMethod { get; init; } = "POST"; // POST, GET
    public Dictionary<string, string>? TokenEndpointHeaders { get; init; }
    public string? TokenEndpointBody { get; init; } // JSON body for token request
    public string? TokenResponsePath { get; init; } // JSON path to extract token (e.g., "access_token" or "data.token")
    public int? TokenExpirySeconds { get; init; } // Optional: cache token for this duration
    
    public Dictionary<string, string>? Headers { get; init; }
    public Dictionary<string, string>? QueryParameters { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public List<ApiEndpoint>? Endpoints { get; init; }
}

public record ApiEndpoint
{
    public required string Method { get; init; } // GET, POST, PUT, DELETE
    public required string Path { get; init; } // e.g., /api/users
    public string? Name { get; init; } // Friendly name
    public string? ResponseDataPath { get; init; } // JSON path to extract data (e.g., data.results)
    public string? RequestDataPath { get; init; } // JSON path for request body (for POST/PUT)
}

// Write configuration models for destination connectors
public record DatabaseWriteConfig
{
    public string? TableName { get; init; }
    public string? Operation { get; init; } // INSERT, UPDATE, UPSERT, BULK_INSERT
    public List<string>? PrimaryKeys { get; init; }
    public int BatchSize { get; init; } = 1000;
}

public record FileWriteConfig
{
    public string? WriteMode { get; init; } // OVERWRITE, APPEND
    public bool IncludeHeaders { get; init; } = true;
    public List<string>? ColumnOrder { get; init; }
    public string? FilenamePattern { get; init; }
    public string? SheetName { get; init; } // For Excel
    public string? StartCell { get; init; } // For Excel
    public string? Structure { get; init; } // For JSON: ARRAY, OBJECT
    public string? RootKey { get; init; } // For JSON
}

public record ApiWriteConfig
{
    public string? RequestFormat { get; init; } // JSON, XML, Form Data
    public bool WrapInArray { get; init; }
    public string? RootKey { get; init; }
    public int BatchSize { get; init; } = 100;
}

/// <summary>
/// Configuration model for Email connector type.
/// The email body contains only a summary; all data rows go into the file attachment.
/// </summary>
public record EmailConfig
{
    /// <summary>Recipient email addresses (required, at least one)</summary>
    public List<string> Recipients { get; init; } = new();

    /// <summary>CC recipient email addresses (optional)</summary>
    public List<string>? CcRecipients { get; init; }

    /// <summary>Email subject line (required)</summary>
    public string? Subject { get; init; }

    /// <summary>Optional custom message included in the email body alongside the auto-generated summary</summary>
    public string? BodyMessage { get; init; }

    /// <summary>File format for the data attachment: CSV, JSON, or Excel</summary>
    public string? AttachmentFormat { get; init; } // CSV, JSON, Excel

    /// <summary>Base filename for the attachment (timestamp is appended automatically)</summary>
    public string? AttachmentFileName { get; init; }
}

/// <summary>
/// Write configuration for Email connector destination.
/// </summary>
public record EmailWriteConfig
{
    /// <summary>Whether to send email when the pipeline produces no data rows</summary>
    public bool SendEmptyReport { get; init; } = false;
}

/// <summary>
/// Request model for previewing the email template with the user's configuration values.
/// </summary>
public record EmailPreviewRequest
{
    /// <summary>Optional custom body message to include in the email</summary>
    public string? BodyMessage { get; init; }

    /// <summary>Attachment format: CSV, JSON, or Excel</summary>
    public string? AttachmentFormat { get; init; }

    /// <summary>Base filename for the attachment</summary>
    public string? AttachmentFileName { get; init; }
}
