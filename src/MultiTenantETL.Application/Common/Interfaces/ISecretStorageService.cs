namespace MultiTenantETL.Application.Common.Interfaces;

/// <summary>
/// Service for managing secrets in a secure storage (Azure Key Vault).
/// Used for storing connector credentials securely outside the database.
/// </summary>
public interface ISecretStorageService
{
    /// <summary>
    /// Stores a secret value in the secure storage.
    /// </summary>
    /// <param name="secretName">The unique name/identifier for the secret</param>
    /// <param name="secretValue">The secret value to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task StoreSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a secret value from the secure storage.
    /// </summary>
    /// <param name="secretName">The unique name/identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The secret value, or null if not found</returns>
    Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secret from the secure storage.
    /// </summary>
    /// <param name="secretName">The unique name/identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a secret exists in the secure storage.
    /// </summary>
    /// <param name="secretName">The unique name/identifier for the secret</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the secret exists, false otherwise</returns>
    Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a standardized secret name for a connector credential field.
    /// Format: {prefix}-{tenantId}-{connectorId}-{fieldName}
    /// </summary>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="connectorId">The connector ID</param>
    /// <param name="fieldName">The credential field name (e.g., "password", "apiKey")</param>
    /// <returns>A standardized, Key Vault-compatible secret name</returns>
    string GenerateSecretName(Guid tenantId, Guid connectorId, string fieldName);
}
