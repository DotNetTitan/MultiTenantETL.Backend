namespace MultiTenantETL.Application.Connectors.DataWriters;

public class DataWriteResult
{
    public Guid BatchId { get; set; }
    public int RowsWritten { get; set; }
    public int RowsFailed { get; set; }
    
    /// <summary>
    /// Batch-level errors (e.g., connection failures, permission issues)
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Per-row errors with row index and error details
    /// </summary>
    public List<RowError> RowErrors { get; set; } = new();
    
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents an error for a specific row
/// </summary>
public class RowError
{
    /// <summary>
    /// Index of the row within the batch (0-based)
    /// </summary>
    public int RowIndex { get; set; }
    
    /// <summary>
    /// The row data that failed (optional, for debugging)
    /// </summary>
    public Dictionary<string, object?>? RowData { get; set; }
    
    /// <summary>
    /// Error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Error code (e.g., "CONSTRAINT_VIOLATION", "TYPE_MISMATCH")
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Column that caused the error (if applicable)
    /// </summary>
    public string? ColumnName { get; set; }
}
