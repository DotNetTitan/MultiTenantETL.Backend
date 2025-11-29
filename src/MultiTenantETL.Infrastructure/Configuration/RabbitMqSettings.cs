namespace MultiTenantETL.Infrastructure.Configuration;

public class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExecutionQueueName { get; set; } = "pipeline-executions";
    public string CancellationQueueName { get; set; } = "pipeline-cancellations";
    public string DeadLetterExchange { get; set; } = "pipeline-dlx";
    public int PrefetchCount { get; set; } = 1;
    public int MaxRetryAttempts { get; set; } = 5;
}
