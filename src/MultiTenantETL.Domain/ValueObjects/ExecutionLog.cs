namespace MultiTenantETL.Domain.ValueObjects;

public class ExecutionLog
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty; // Info, Warning, Error
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public ExecutionLog()
    {
        Timestamp = DateTime.UtcNow;
    }

    public ExecutionLog(string level, string message, string? details = null)
    {
        Timestamp = DateTime.UtcNow;
        Level = level;
        Message = message;
        Details = details;
    }

    public static ExecutionLog Info(string message, string? details = null)
        => new("Info", message, details);

    public static ExecutionLog Warning(string message, string? details = null)
        => new("Warning", message, details);

    public static ExecutionLog Error(string message, string? details = null)
        => new("Error", message, details);
}
