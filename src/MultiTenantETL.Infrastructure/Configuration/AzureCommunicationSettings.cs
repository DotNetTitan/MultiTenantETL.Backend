namespace MultiTenantETL.Infrastructure.Configuration
{
    /// <summary>
    /// Strongly-typed configuration for Azure Communication Services
    /// </summary>
    public class AzureCommunicationSettings
    {
        public string? ConnectionString { get; set; }
        public string? SenderEmailAddress { get; set; }
    }
}