using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Connectors.DataWriters;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableCors("AllowFrontend")]
public class FormatValidationController : ControllerBase
{
    private readonly IFormatValidator _formatValidator;
    private readonly ILogger<FormatValidationController> _logger;

    public FormatValidationController(
        IFormatValidator formatValidator,
        ILogger<FormatValidationController> logger)
    {
        _formatValidator = formatValidator;
        _logger = logger;
    }

    /// <summary>
    /// Validates a format choice and provides recommendations
    /// </summary>
    /// <param name="request">Format validation request</param>
    /// <returns>Recommendation with severity and alternatives</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(FormatRecommendation), StatusCodes.Status200OK)]
    public IActionResult ValidateFormat([FromBody] FormatValidationRequest request)
    {
        var context = new WriteContext
        {
            EstimatedRowCount = request.EstimatedRowCount,
            IsAppendMode = request.IsAppendMode,
            IsTruncateMode = request.IsTruncateMode,
            DestinationType = request.DestinationType,
            RequiredFormat = request.RequiredFormat,
            RequiresReadability = request.RequiresReadability,
            IsProduction = request.IsProduction,
            Frequency = request.Frequency
        };

        var recommendation = _formatValidator.ValidateFormat(request.Format, context);

        _logger.LogInformation(
            "Format validation: {Format} for {DestinationType} with {RowCount} rows - Level: {Level}",
            request.Format,
            request.DestinationType,
            request.EstimatedRowCount,
            recommendation.Level);

        return Ok(recommendation);
    }

    /// <summary>
    /// Gets the recommended format for a given context
    /// </summary>
    /// <param name="request">Context information</param>
    /// <returns>Recommended format</returns>
    [HttpPost("recommend")]
    [ProducesResponseType(typeof(FormatRecommendationResponse), StatusCodes.Status200OK)]
    public IActionResult GetRecommendedFormat([FromBody] FormatRecommendationRequest request)
    {
        var context = new WriteContext
        {
            EstimatedRowCount = request.EstimatedRowCount,
            IsAppendMode = request.IsAppendMode,
            IsTruncateMode = request.IsTruncateMode,
            DestinationType = request.DestinationType,
            RequiredFormat = request.RequiredFormat,
            RequiresReadability = request.RequiresReadability,
            IsProduction = request.IsProduction,
            Frequency = request.Frequency
        };

        var recommendedFormat = _formatValidator.GetRecommendedFormat(context);
        var validation = _formatValidator.ValidateFormat(recommendedFormat, context);

        var response = new FormatRecommendationResponse
        {
            RecommendedFormat = recommendedFormat,
            Reason = validation.Reason ?? "Optimal for the given context",
            Alternatives = GetAlternativeFormats(context, recommendedFormat)
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets all supported formats with their characteristics
    /// </summary>
    [HttpGet("formats")]
    [ProducesResponseType(typeof(List<FormatInfo>), StatusCodes.Status200OK)]
    public IActionResult GetSupportedFormats()
    {
        var formats = new List<FormatInfo>
        {
            new()
            {
                Format = "csv",
                DisplayName = "CSV (Comma-Separated Values)",
                Description = "Human-readable, widely compatible format",
                SupportsAppend = true,
                SupportsStreaming = true,
                BestFor = new[] { "General use", "Excel compatibility", "Human readability" },
                MaxRecommendedRows = 10_000_000
            },
            new()
            {
                Format = "json",
                DisplayName = "JSON (Array)",
                Description = "Standard JSON array format",
                SupportsAppend = false,
                SupportsStreaming = false,
                BestFor = new[] { "Small datasets", "API responses", "Single writes" },
                MaxRecommendedRows = 10_000,
                Warnings = new[] { "Append mode requires loading entire file into memory" }
            },
            new()
            {
                Format = "jsonl",
                DisplayName = "JSONL (JSON Lines)",
                Description = "Newline-delimited JSON, one object per line",
                SupportsAppend = true,
                SupportsStreaming = true,
                BestFor = new[] { "Large datasets", "Streaming", "Cloud storage", "Append operations" },
                MaxRecommendedRows = null // No practical limit
            },
            new()
            {
                Format = "parquet",
                DisplayName = "Parquet",
                Description = "Columnar storage format with compression",
                SupportsAppend = false,
                SupportsStreaming = true,
                BestFor = new[] { "Very large datasets", "Analytics", "Data warehouses" },
                MaxRecommendedRows = null
            },
            new()
            {
                Format = "excel",
                DisplayName = "Excel (XLSX)",
                Description = "Microsoft Excel format",
                SupportsAppend = false,
                SupportsStreaming = false,
                BestFor = new[] { "Small reports", "Manual review", "Business users" },
                MaxRecommendedRows = 100_000,
                Warnings = new[] { "Limited to ~1 million rows", "Poor performance with large datasets" }
            }
        };

        return Ok(formats);
    }

    private List<string> GetAlternativeFormats(WriteContext context, string recommendedFormat)
    {
        var alternatives = new List<string>();

        // Always include JSONL as an alternative for file destinations
        if (recommendedFormat != "jsonl" &&
            !context.DestinationType.Equals("API", StringComparison.OrdinalIgnoreCase))
        {
            alternatives.Add("jsonl");
        }

        // CSV is a good general alternative
        if (recommendedFormat != "csv")
        {
            alternatives.Add("csv");
        }

        // JSON for small datasets or APIs
        if (recommendedFormat != "json" &&
            (context.EstimatedRowCount < 10_000 ||
             context.DestinationType.Equals("API", StringComparison.OrdinalIgnoreCase)))
        {
            alternatives.Add("json");
        }

        return alternatives;
    }
}

// DTOs for API requests/responses
public class FormatValidationRequest
{
    public string Format { get; set; } = string.Empty;
    public long? EstimatedRowCount { get; set; }
    public bool IsAppendMode { get; set; }
    public bool IsTruncateMode { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? RequiredFormat { get; set; }
    public bool RequiresReadability { get; set; }
    public bool IsProduction { get; set; }
    public WriteFrequency Frequency { get; set; }
}

public class FormatRecommendationRequest
{
    public long? EstimatedRowCount { get; set; }
    public bool IsAppendMode { get; set; }
    public bool IsTruncateMode { get; set; }
    public string DestinationType { get; set; } = string.Empty;
    public string? RequiredFormat { get; set; }
    public bool RequiresReadability { get; set; }
    public bool IsProduction { get; set; }
    public WriteFrequency Frequency { get; set; }
}

public class FormatRecommendationResponse
{
    public string RecommendedFormat { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Alternatives { get; set; } = new();
}

public class FormatInfo
{
    public string Format { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool SupportsAppend { get; set; }
    public bool SupportsStreaming { get; set; }
    public string[] BestFor { get; set; } = Array.Empty<string>();
    public long? MaxRecommendedRows { get; set; }
    public string[]? Warnings { get; set; }
}
