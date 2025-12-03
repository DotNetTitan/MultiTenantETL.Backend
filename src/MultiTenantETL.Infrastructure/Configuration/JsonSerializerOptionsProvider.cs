using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Provides centralized JsonSerializerOptions for the entire application.
/// This ensures consistent JSON serialization/deserialization behavior across all components.
/// </summary>
public static class JsonSerializerOptionsProvider
{
    /// <summary>
    /// Default options for API serialization with camelCase naming policy.
    /// Use this for serializing/deserializing DTOs and API responses.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Options optimized for high-performance streaming scenarios.
    /// Use this for data readers/writers that process large datasets.
    /// </summary>
    public static JsonSerializerOptions Streaming { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultBufferSize = 81920 // 80KB buffer for streaming
    };

    /// <summary>
    /// Configures the provided JsonSerializerOptions with the default settings.
    /// Use this to configure ASP.NET Core's JsonOptions.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }
}
