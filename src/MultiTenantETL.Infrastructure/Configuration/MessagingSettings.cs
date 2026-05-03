namespace MultiTenantETL.Infrastructure.Configuration;

public class MessagingSettings
{
    public const string SectionName = "Messaging";
    
    /// <summary>
    /// The messaging provider to use: "RabbitMQ" for local development, "ServiceBus" or "StorageQueue" for Azure deployment
    /// </summary>
    public string Provider { get; set; } = "RabbitMQ";
    
    public bool UseRabbitMQ => Provider?.Equals("RabbitMQ", StringComparison.OrdinalIgnoreCase) ?? true;
    public bool UseServiceBus => Provider?.Equals("ServiceBus", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool UseStorageQueue => Provider?.Equals("StorageQueue", StringComparison.OrdinalIgnoreCase) ?? false;
}
