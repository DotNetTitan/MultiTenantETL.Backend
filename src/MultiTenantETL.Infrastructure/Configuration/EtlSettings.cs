namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for ETL operations
/// </summary>
public class EtlSettings
{
    /// <summary>
    /// Default batch size for database operations (SQL Server, PostgreSQL)
    /// </summary>
    public int DefaultBatchSize { get; set; } = 5000;

    /// <summary>
    /// Batch size for MySQL operations (lower due to parameter limits)
    /// </summary>
    public int MySqlBatchSize { get; set; } = 1000;

    /// <summary>
    /// Batch size for file operations (CSV, JSON, NDJSON)
    /// </summary>
    public int FileBatchSize { get; set; } = 5000;

    /// <summary>
    /// Default timeout in seconds for database operations
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of concurrent batch operations
    /// </summary>
    public int MaxConcurrentBatches { get; set; } = 1;

    /// <summary>
    /// Buffer size for file streaming operations (in bytes)
    /// </summary>
    public int FileStreamBufferSize { get; set; } = 8192;

    /// <summary>
    /// Enable detailed logging for ETL operations
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Maximum rows to process in a single execution (0 = unlimited)
    /// </summary>
    public int MaxRowsPerExecution { get; set; } = 0;
}
