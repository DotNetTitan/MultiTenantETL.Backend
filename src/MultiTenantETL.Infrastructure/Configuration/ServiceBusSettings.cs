namespace MultiTenantETL.Infrastructure.Configuration;

public class ServiceBusSettings
{
    public string? ConnectionString { get; set; }
    public string ExecutionQueueName { get; set; } = "pipeline-executions";
    public string CancellationQueueName { get; set; } = "pipeline-cancellations";
    public int PrefetchCount { get; set; } = 1;
    public int MaxRetryAttempts { get; set; } = 5;
    public int MaxConcurrentCalls { get; set; } = 1;
}
