namespace MultiTenantETL.Application.Connectors.DataWriters;

/// <summary>
/// Validates and provides recommendations for data format selection
/// </summary>
public interface IFormatValidator
{
    /// <summary>
    /// Validates a format choice and provides recommendations
    /// </summary>
    /// <param name="format">The selected format (csv, json, jsonl, etc.)</param>
    /// <param name="context">Context about the write operation</param>
    /// <returns>Recommendation with severity level and alternative suggestions</returns>
    FormatRecommendation ValidateFormat(string format, WriteContext context);
    
    /// <summary>
    /// Gets the recommended format for a given context
    /// </summary>
    /// <param name="context">Context about the write operation</param>
    /// <returns>The optimal format for the use case</returns>
    string GetRecommendedFormat(WriteContext context);
}

/// <summary>
/// Context information for format validation
/// </summary>
public class WriteContext
{
    /// <summary>
    /// Estimated number of rows to write
    /// </summary>
    public long? EstimatedRowCount { get; set; }
    
    /// <summary>
    /// Whether the operation will append to existing data
    /// </summary>
    public bool IsAppendMode { get; set; }
    
    /// <summary>
    /// Whether the operation will truncate before writing
    /// </summary>
    public bool IsTruncateMode { get; set; }
    
    /// <summary>
    /// Destination type (File, S3, AzureBlob, Database, API)
    /// </summary>
    public string DestinationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the destination requires a specific format
    /// </summary>
    public string? RequiredFormat { get; set; }
    
    /// <summary>
    /// Whether human readability is a priority
    /// </summary>
    public bool RequiresReadability { get; set; }
    
    /// <summary>
    /// Whether this is a production pipeline
    /// </summary>
    public bool IsProduction { get; set; }
    
    /// <summary>
    /// Expected frequency of writes (once, hourly, daily, etc.)
    /// </summary>
    public WriteFrequency Frequency { get; set; }
}

public enum WriteFrequency
{
    Once,
    Hourly,
    Daily,
    Weekly,
    Continuous
}
