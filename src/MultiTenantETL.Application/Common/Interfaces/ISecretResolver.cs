using System.Text.Json;

namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Service for resolving Key Vault secret references in connector configurations.
/// Converts references like "keyvault:secret-name" to actual secret values.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves all Key Vault secret references in a JSON configuration.
    /// Replaces "keyvault:{secretName}" references with actual secret values.
    /// </summary>
    /// <param name="configJson">The JSON configuration containing potential secret references</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON element with resolved secret values</returns>
    Task<JsonElement> ResolveSecretsAsync(string configJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a configuration contains any Key Vault secret references.
    /// </summary>
    /// <param name="configJson">The JSON configuration to check</param>
    /// <returns>True if the configuration contains secret references, false otherwise</returns>
    bool ContainsSecretReferences(string configJson);
}
