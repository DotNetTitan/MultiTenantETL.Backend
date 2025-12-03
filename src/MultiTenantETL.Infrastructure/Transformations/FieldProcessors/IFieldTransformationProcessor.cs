using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Transformations.FieldProcessors;

/// <summary>
/// Interface for applying transformations to individual field values
/// </summary>
public interface IFieldTransformationProcessor
{
    /// <summary>
    /// Apply a transformation to a field value
    /// </summary>
    /// <param name="value">The value to transform (can be single value or list for multiple source fields)</param>
    /// <param name="transformation">The transformation configuration</param>
    /// <param name="sourceFields">The source field names (for context)</param>
    /// <returns>Transformed value</returns>
    object? ApplyTransformation(object? value, TransformationConfig transformation, List<string> sourceFields);

    /// <summary>
    /// Apply a transformation to a field value with full row context
    /// </summary>
    /// <param name="value">The value to transform (can be single value or list for multiple source fields)</param>
    /// <param name="transformation">The transformation configuration</param>
    /// <param name="sourceFields">The source field names (for context)</param>
    /// <param name="row">The full row data for script transformations that need row access</param>
    /// <returns>Transformed value</returns>
    object? ApplyTransformation(object? value, TransformationConfig transformation, List<string> sourceFields, Dictionary<string, object?>? row);
}

/// <summary>
/// Transformation configuration for field-level transformations
/// </summary>
public class TransformationConfig
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public JsonElement? Config { get; set; }
    public int Order { get; set; }
    public bool IsEnabled { get; set; } = true;
}
