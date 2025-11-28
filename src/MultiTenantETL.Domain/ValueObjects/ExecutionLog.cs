namespace MultiTenantETL.Domain.ValueObjects;

public class ExecutionLog
{
    public DateTimeOffset Timestamp { get; set; }
    public required string Level { get; set; } // Info, Warning, Error, Debug
    public required string Source { get; set; }
    public required string Message { get; set; }
    public string? Details { get; set; }

    public static ExecutionLog Info(string message, string? details = null, string source = "System")
    {
        return new ExecutionLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Info",
            Source = source,
            Message = message,
            Details = details
        };
    }

    public static ExecutionLog Warning(string message, string? details = null, string source = "System")
    {
        return new ExecutionLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Warning",
            Source = source,
            Message = message,
            Details = details
        };
    }

    public static ExecutionLog Error(string message, string? details = null, string source = "System")
    {
        return new ExecutionLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Error",
            Source = source,
            Message = message,
            Details = details
        };
    }

    public static ExecutionLog Debug(string message, string? details = null, string source = "System")
    {
        return new ExecutionLog
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Debug",
            Source = source,
            Message = message,
            Details = details
        };
    }
}
