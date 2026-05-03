namespace MultiTenantETL.Domain.ValueObjects;

/// <summary>
/// Represents a log entry during pipeline execution.
/// </summary>
public class ExecutionLog
{
    /// <summary>
    /// When the log entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Log level (Info, Warning, Error, Debug).
    /// </summary>
    public required string Level { get; set; }

    /// <summary>
    /// Source component that created this entry.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Log message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Additional details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Creates an info-level log entry.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="source">The source component.</param>
    /// <returns>A new ExecutionLog instance.</returns>
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

    /// <summary>
    /// Creates a warning-level log entry.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="source">The source component.</param>
    /// <returns>A new ExecutionLog instance.</returns>
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

    /// <summary>
    /// Creates an error-level log entry.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="source">The source component.</param>
    /// <returns>A new ExecutionLog instance.</returns>
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

    /// <summary>
    /// Creates a debug-level log entry.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="details">Optional details.</param>
    /// <param name="source">The source component.</param>
    /// <returns>A new ExecutionLog instance.</returns>
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
