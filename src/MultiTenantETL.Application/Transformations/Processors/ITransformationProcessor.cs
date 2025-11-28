namespace MultiTenantETL.Application.Transformations.Processors;

/// <summary>
/// Interface for processing data transformations
/// </summary>
public interface ITransformationProcessor
{
    /// <summary>
    /// The transformation type this processor handles (e.g., "Filter", "Map", "Trim")
    /// </summary>
    string TransformationType { get; }

    /// <summary>
    /// Process a batch of rows through the transformation
    /// </summary>
    Task<TransformationResult> ProcessAsync(
        List<Dictionary<string, object?>> data,
        string configJson,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transformation operation
/// </summary>
public class TransformationResult
{
    public List<Dictionary<string, object?>> Data { get; set; } = new();
    public int RowsInput { get; set; }
    public int RowsOutput { get; set; }
    public int RowsFiltered { get; set; }
    public int RowsModified { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsSuccess => Errors.Count == 0;
}
