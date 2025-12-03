namespace MultiTenantETL.Infrastructure.Configuration;

public class RabbitMqSettings
{
    public string? ConnectionString { get; set; }
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

    /// <summary>
    /// Creates a ConnectionFactory configured with either the connection string (if available)
    /// or the individual settings.
    /// </summary>
    public RabbitMQ.Client.ConnectionFactory CreateConnectionFactory()
    {
        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            DispatchConsumersAsync = true
        };

        if (!string.IsNullOrEmpty(ConnectionString))
        {
            // When using Aspire, connection string is in amqp:// URI format
            factory.Uri = new Uri(ConnectionString);
        }
        else
        {
            // Fall back to individual settings for manual configuration
            factory.HostName = HostName;
            factory.Port = Port;
            factory.UserName = UserName;
            factory.Password = Password;
            factory.VirtualHost = VirtualHost;
        }

        return factory;
    }
}
