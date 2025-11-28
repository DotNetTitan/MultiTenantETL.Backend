namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Represents a format recommendation for data writing operations
/// </summary>
public class FormatRecommendation
{
    public string Format { get; set; } = string.Empty;
    public RecommendationLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AlternativeFormat { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Severity level of the recommendation
/// </summary>
public enum RecommendationLevel
{
    /// <summary>
    /// Format is optimal for the use case
    /// </summary>
    Optimal = 0,
    
    /// <summary>
    /// Format will work but there's a better option
    /// </summary>
    Suggestion = 1,
    
    /// <summary>
    /// Format may cause performance issues
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Format is not recommended and may fail
    /// </summary>
    Error = 3
}
