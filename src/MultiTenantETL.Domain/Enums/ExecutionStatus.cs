namespace MultiTenantETL.Domain.Enums;

/// <summary>
/// Status of a pipeline execution.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>The execution has been queued for processing.</summary>
    Queued,

    /// <summary>The execution is currently running.</summary>
    Running,

    /// <summary>The execution completed successfully.</summary>
    Completed,

    /// <summary>The execution failed.</summary>
    Failed,

    /// <summary>The execution was cancelled.</summary>
    Cancelled
}
