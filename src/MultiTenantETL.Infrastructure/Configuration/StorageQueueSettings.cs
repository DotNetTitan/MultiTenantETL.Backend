namespace MultiTenantETL.Infrastructure.Configuration;

public class StorageQueueSettings
{
    public string? ConnectionString { get; set; }
    public string ExecutionQueueName { get; set; } = "pipeline-executions";
    public string CancellationQueueName { get; set; } = "pipeline-cancellations";
    public TimeSpan MessageVisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxDequeueCount { get; set; } = 3;
    public int MaxConcurrentReceives { get; set; } = 1;
    public TimeSpan PollingIntervalMs { get; set; } = TimeSpan.FromSeconds(1);
}
