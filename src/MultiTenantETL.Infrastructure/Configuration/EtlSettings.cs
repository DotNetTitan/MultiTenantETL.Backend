namespace MultiTenantETL.Infrastructure.Configuration;

public class EtlSettings
{
    public const string SectionName = "EtlSettings";

    /// <summary>
    /// Default batch size for reading data from sources
    /// </summary>
    public int DefaultBatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum batch size allowed
    /// </summary>
    public int MaxBatchSize { get; set; } = 10000;

    /// <summary>
    /// Command timeout in seconds for database operations
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Buffer size for streaming file operations (bytes)
    /// </summary>
    public int StreamBufferSize { get; set; } = 81920; // 80KB

    /// <summary>
    /// MySQL multi-row insert chunk size
    /// </summary>
    public int MySqlBulkInsertChunkSize { get; set; } = 1000;

    /// <summary>
    /// Maximum rows to process in a single execution (0 = unlimited)
    /// </summary>
    public int MaxRowsPerExecution { get; set; } = 0;

    /// <summary>
    /// Enable detailed execution logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Execution log retention days
    /// </summary>
    public int LogRetentionDays { get; set; } = 365;
}
