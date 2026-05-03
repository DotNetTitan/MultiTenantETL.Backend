using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataWriters;

namespace MultiTenantETL.Infrastructure.DataWriters;

/// <summary>
/// Validates data format choices and provides intelligent recommendations
/// </summary>
public class FormatValidator : IFormatValidator
{
    private readonly ILogger<FormatValidator> _logger;
    
    // Thresholds for recommendations
    private const long LargeDatasetThreshold = 10_000;
    private const long VeryLargeDatasetThreshold = 100_000;

    public FormatValidator(ILogger<FormatValidator> logger)
    {
        _logger = logger;
    }

    public FormatRecommendation ValidateFormat(string format, WriteContext context)
    {
        var normalizedFormat = format.ToLower();

        return normalizedFormat switch
        {
            "json" => ValidateJsonFormat(context),
            "jsonl" or "jsonlines" or "ndjson" => ValidateJsonLinesFormat(context),
            "csv" => ValidateCsvFormat(context),
            "parquet" => ValidateParquetFormat(context),
            "excel" or "xlsx" => ValidateExcelFormat(context),
            _ => new FormatRecommendation
            {
                Format = format,
                Level = RecommendationLevel.Warning,
                Message = $"Format '{format}' is not recognized. Supported formats: CSV, JSON, JSONL, Parquet, Excel",
                Reason = "Unknown format"
            }
        };
    }

    public string GetRecommendedFormat(WriteContext context)
    {
        // API destinations typically require JSON
        if (context.DestinationType.Equals("API", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        // If a specific format is required, use it
        if (!string.IsNullOrEmpty(context.RequiredFormat))
        {
            return context.RequiredFormat;
        }

        // Database destinations don't use file formats
        if (context.DestinationType.Equals("Database", StringComparison.OrdinalIgnoreCase))
        {
            return "database";
        }

        // For large datasets or append mode, prefer JSONL
        if (context.EstimatedRowCount > LargeDatasetThreshold || context.IsAppendMode)
        {
            return "jsonl";
        }

        // For very large datasets, consider Parquet
        if (context.EstimatedRowCount > VeryLargeDatasetThreshold && context.IsProduction)
        {
            return "parquet";
        }

        // For human readability, prefer CSV
        if (context.RequiresReadability)
        {
            return "csv";
        }

        // Default to JSONL for cloud storage (Azure Blob)
        if (context.DestinationType.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return "jsonl";
        }

        // Default to CSV for general use
        return "csv";
    }

    private FormatRecommendation ValidateJsonFormat(WriteContext context)
    {
        // JSON append with large datasets is problematic
        if (context.IsAppendMode && context.EstimatedRowCount > LargeDatasetThreshold)
        {
            return new FormatRecommendation
            {
                Format = "json",
                Level = RecommendationLevel.Error,
                Message = "JSON format with append mode requires loading the entire file into memory. This will cause performance issues or failures with large datasets.",
                AlternativeFormat = "jsonl",
                Reason = "JSON arrays cannot be appended efficiently. Each append requires reading and rewriting the entire file."
            };
        }

        if (context.IsAppendMode)
        {
            return new FormatRecommendation
            {
                Format = "json",
                Level = RecommendationLevel.Warning,
                Message = "JSON format with append mode may cause performance issues as the file grows.",
                AlternativeFormat = "jsonl",
                Reason = "JSON arrays require reading the entire file for each append operation."
            };
        }

        // JSON for large single writes is acceptable but not optimal
        if (context.EstimatedRowCount > VeryLargeDatasetThreshold && context.IsTruncateMode)
        {
            return new FormatRecommendation
            {
                Format = "json",
                Level = RecommendationLevel.Suggestion,
                Message = "For very large datasets, consider JSONL or Parquet for better performance.",
                AlternativeFormat = "jsonl",
                Reason = "JSONL provides better streaming performance for large datasets."
            };
        }

        // JSON is fine for small datasets or single writes
        return new FormatRecommendation
        {
            Format = "json",
            Level = RecommendationLevel.Optimal,
            Message = "JSON format is suitable for this use case.",
            Reason = "Small dataset or single write operation."
        };
    }

    private FormatRecommendation ValidateJsonLinesFormat(WriteContext context)
    {
        // JSONL is optimal for most scenarios except APIs
        if (context.DestinationType.Equals("API", StringComparison.OrdinalIgnoreCase))
        {
            return new FormatRecommendation
            {
                Format = "jsonl",
                Level = RecommendationLevel.Warning,
                Message = "Most APIs expect JSON arrays, not JSONL format.",
                AlternativeFormat = "json",
                Reason = "API endpoints typically require standard JSON format."
            };
        }

        return new FormatRecommendation
        {
            Format = "jsonl",
            Level = RecommendationLevel.Optimal,
            Message = "JSONL format is excellent for streaming large datasets and supports efficient append operations.",
            Reason = "JSONL provides optimal performance for large-scale data operations."
        };
    }

    private FormatRecommendation ValidateCsvFormat(WriteContext context)
    {
        // CSV is generally good for most use cases
        if (context.EstimatedRowCount > VeryLargeDatasetThreshold && context.IsProduction)
        {
            return new FormatRecommendation
            {
                Format = "csv",
                Level = RecommendationLevel.Suggestion,
                Message = "For very large production datasets, consider Parquet for better compression and performance.",
                AlternativeFormat = "parquet",
                Reason = "Parquet provides columnar storage with better compression for analytics workloads."
            };
        }

        return new FormatRecommendation
        {
            Format = "csv",
            Level = RecommendationLevel.Optimal,
            Message = "CSV format is suitable for this use case and provides good readability.",
            Reason = "CSV is widely compatible and human-readable."
        };
    }

    private FormatRecommendation ValidateParquetFormat(WriteContext context)
    {
        // Parquet is excellent for large datasets but requires additional libraries
        return new FormatRecommendation
        {
            Format = "parquet",
            Level = RecommendationLevel.Optimal,
            Message = "Parquet format provides excellent compression and performance for large datasets.",
            Reason = "Columnar storage format optimized for analytics."
        };
    }

    private FormatRecommendation ValidateExcelFormat(WriteContext context)
    {
        // Excel has row limits and performance issues with large datasets
        if (context.EstimatedRowCount > 100_000)
        {
            return new FormatRecommendation
            {
                Format = "excel",
                Level = RecommendationLevel.Error,
                Message = "Excel format has a limit of ~1 million rows and poor performance with large datasets.",
                AlternativeFormat = "csv",
                Reason = "Excel is not designed for large-scale data operations."
            };
        }

        if (context.EstimatedRowCount > 10_000)
        {
            return new FormatRecommendation
            {
                Format = "excel",
                Level = RecommendationLevel.Warning,
                Message = "Excel format may have performance issues with datasets over 10,000 rows.",
                AlternativeFormat = "csv",
                Reason = "Excel is optimized for smaller datasets and manual editing."
            };
        }

        return new FormatRecommendation
        {
            Format = "excel",
            Level = RecommendationLevel.Optimal,
            Message = "Excel format is suitable for small datasets that require manual review.",
            Reason = "Good for human-readable reports and small datasets."
        };
    }
}
