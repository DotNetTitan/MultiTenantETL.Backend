namespace MultiTenantETL.Domain.Enums;

public enum ExecutionStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
