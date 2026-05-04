using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Constants;
using System.Text.Json;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Helper service for resolving Key Vault secret references in connector configurations.
/// Converts references like "keyvault:secret-name" to actual secret values.
/// </summary>
public class SecretResolver : ISecretResolver
{
    private readonly ISecretStorageService _secretStorageService;
    private readonly ILogger<SecretResolver> _logger;

    public SecretResolver(
        ISecretStorageService secretStorageService,
        ILogger<SecretResolver> logger)
    {
        _secretStorageService = secretStorageService ?? throw new ArgumentNullException(nameof(secretStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves all Key Vault secret references in a JSON configuration.
    /// Replaces "keyvault:{secretName}" references with actual secret values.
    /// </summary>
    /// <param name="configJson">The JSON configuration containing potential secret references</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON element with resolved secret values</returns>
    public async Task<JsonElement> ResolveSecretsAsync(string configJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            throw new ArgumentException("Configuration JSON cannot be null or whitespace", nameof(configJson));
        }

        try
        {
            var jsonDoc = JsonDocument.Parse(configJson);
            var root = jsonDoc.RootElement;

            var resolved = await ResolveElementAsync(root, cancellationToken);

            var serialized = JsonSerializer.Serialize(resolved);
            return JsonDocument.Parse(serialized).RootElement;
        }
        catch (JsonException)
        {
            // Re-throw JsonException without wrapping
            throw;
        }
        catch (KeyNotFoundException)
        {
            // Re-throw KeyNotFoundException without wrapping
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve secrets in configuration");
            throw new InvalidOperationException("Failed to resolve Key Vault secrets", ex);
        }
    }

    /// <summary>
    /// Recursively resolves secret references in a JSON element.
    /// </summary>
    private async Task<object?> ResolveElementAsync(JsonElement element, CancellationToken cancellationToken)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var stringValue = element.GetString();

                if (!string.IsNullOrWhiteSpace(stringValue) &&
                    stringValue.StartsWith(EncryptionConstants.SecretReferencePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract secret name from reference
                    var secretName = stringValue.Substring(EncryptionConstants.SecretReferencePrefix.Length);

                    _logger.LogDebug("Resolving Key Vault reference: {SecretName}", secretName);

                    // Retrieve actual secret value
                    var secretValue = await _secretStorageService.GetSecretAsync(secretName, cancellationToken);

                    if (secretValue == null)
                    {
                        _logger.LogError("Secret not found in Key Vault: {SecretName}", secretName);
                        throw new KeyNotFoundException($"Secret '{secretName}' not found in Key Vault");
                    }

                    _logger.LogDebug("Successfully resolved secret: {SecretName}", secretName);
                    return secretValue;
                }

                return stringValue;

            case JsonValueKind.Object:
                var objDict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    objDict[property.Name] = await ResolveElementAsync(property.Value, cancellationToken);
                }
                return objDict;

            case JsonValueKind.Array:
                var array = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(await ResolveElementAsync(item, cancellationToken));
                }
                return array;

            case JsonValueKind.Number:
                return element.TryGetInt64(out var l) ? l : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                return element.GetRawText();
        }
    }

    /// <summary>
    /// Checks if a configuration contains any Key Vault secret references.
    /// </summary>
    /// <param name="configJson">The JSON configuration to check</param>
    /// <returns>True if the configuration contains secret references</returns>
    public bool ContainsSecretReferences(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return false;
        }

        try
        {
            var jsonDoc = JsonDocument.Parse(configJson);
            return ContainsSecretReferencesInElement(jsonDoc.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Recursively checks if a JSON element contains secret references.
    /// </summary>
    private bool ContainsSecretReferencesInElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var stringValue = element.GetString();
                return !string.IsNullOrWhiteSpace(stringValue) &&
                       stringValue.StartsWith(EncryptionConstants.SecretReferencePrefix, StringComparison.OrdinalIgnoreCase);

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (ContainsSecretReferencesInElement(property.Value))
                    {
                        return true;
                    }
                }
                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsSecretReferencesInElement(item))
                    {
                        return true;
                    }
                }
                return false;

            default:
                return false;
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()),
            JsonValueKind.Array => JsonSerializer.Deserialize<List<object>>(element.GetRawText()),
            _ => element.GetRawText()
        };
    }
}
