namespace MultiTenantETL.Application.Connectors.DataReaders;

/// <summary>
/// Result of a connection test
/// </summary>
public class ConnectionTestResult
{
    public bool IsSuccessful { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
