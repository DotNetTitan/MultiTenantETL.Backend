namespace MultiTenantETL.Domain.Enums;

/// <summary>
/// Status of a batch within a pipeline execution.
/// </summary>
public enum BatchStatus
{
    /// <summary>The batch has been queued for processing.</summary>
    Queued,

    /// <summary>The batch is currently being processed.</summary>
    Processing,

    /// <summary>The batch completed successfully.</summary>
    Completed,

    /// <summary>The batch failed.</summary>
    Failed
}
