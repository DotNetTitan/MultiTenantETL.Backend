using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Infrastructure.Configuration;
using System.Text.RegularExpressions;

namespace MultiTenantETL.Infrastructure.Security;

/// <summary>
/// Implementation of ISecretStorageService using Azure Key Vault.
/// Provides secure storage for connector credentials outside the database.
/// </summary>
public class KeyVaultSecretStorageService : ISecretStorageService
{
    private readonly SecretClient _secretClient;
    private readonly AzureKeyVaultSettings _settings;
    private readonly ILogger<KeyVaultSecretStorageService> _logger;

    public KeyVaultSecretStorageService(
        IOptions<AzureKeyVaultSettings> settings,
        ILogger<KeyVaultSecretStorageService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate settings
        // _settings.Validate();

        // Create SecretClient using DefaultAzureCredential
        // This works with Managed Identity in Azure and developer credentials locally
        var vaultUri = new Uri(_settings.VaultUri);
        _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());

        _logger.LogInformation("KeyVaultSecretStorageService initialized with vault: {VaultUri}", _settings.VaultUri);
    }

    public async Task StoreSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name cannot be null or whitespace", nameof(secretName));
        }

        if (string.IsNullOrWhiteSpace(secretValue))
        {
            throw new ArgumentException("Secret value cannot be null or whitespace", nameof(secretValue));
        }

        try
        {
            _logger.LogDebug("Storing secret: {SecretName}", secretName);

            var secret = new KeyVaultSecret(secretName, secretValue);
            await _secretClient.SetSecretAsync(secret, cancellationToken);

            _logger.LogInformation("Successfully stored secret: {SecretName}", secretName);
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(ex, "Access denied when storing secret {SecretName}. Check Key Vault access policies.", secretName);
            throw new UnauthorizedAccessException(
                $"Access denied to Azure Key Vault. Ensure the application has 'Set' permission for secrets. Secret: {secretName}", ex);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Key Vault request failed when storing secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to store secret in Azure Key Vault: {secretName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to store secret: {secretName}", ex);
        }
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name cannot be null or whitespace", nameof(secretName));
        }

        try
        {
            _logger.LogDebug("Retrieving secret: {SecretName}", secretName);

            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);

            _logger.LogDebug("Successfully retrieved secret: {SecretName}", secretName);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret not found: {SecretName}", secretName);
            return null;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(ex, "Access denied when retrieving secret {SecretName}. Check Key Vault access policies.", secretName);
            throw new UnauthorizedAccessException(
                $"Access denied to Azure Key Vault. Ensure the application has 'Get' permission for secrets. Secret: {secretName}", ex);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Key Vault request failed when retrieving secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to retrieve secret from Azure Key Vault: {secretName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to retrieve secret: {secretName}", ex);
        }
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name cannot be null or whitespace", nameof(secretName));
        }

        try
        {
            _logger.LogDebug("Deleting secret: {SecretName}", secretName);

            // Start the delete operation (soft delete in Key Vault)
            var operation = await _secretClient.StartDeleteSecretAsync(secretName, cancellationToken);

            // Wait for deletion to complete
            await operation.WaitForCompletionAsync(cancellationToken);

            _logger.LogInformation("Successfully deleted secret: {SecretName}", secretName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret not found for deletion: {SecretName}", secretName);
            // Not an error - secret doesn't exist, which is the desired end state
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(ex, "Access denied when deleting secret {SecretName}. Check Key Vault access policies.", secretName);
            throw new UnauthorizedAccessException(
                $"Access denied to Azure Key Vault. Ensure the application has 'Delete' permission for secrets. Secret: {secretName}", ex);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Key Vault request failed when deleting secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to delete secret from Azure Key Vault: {secretName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting secret {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to delete secret: {secretName}", ex);
        }
    }

    public async Task<bool> SecretExistsAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new ArgumentException("Secret name cannot be null or whitespace", nameof(secretName));
        }

        try
        {
            _logger.LogDebug("Checking if secret exists: {SecretName}", secretName);

            // Try to get the secret properties (metadata only, not the value)
            await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);

            _logger.LogDebug("Secret exists: {SecretName}", secretName);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Secret does not exist: {SecretName}", secretName);
            return false;
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogError(ex, "Access denied when checking secret existence {SecretName}. Check Key Vault access policies.", secretName);
            throw new UnauthorizedAccessException(
                $"Access denied to Azure Key Vault. Ensure the application has 'Get' permission for secrets. Secret: {secretName}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking secret existence {SecretName}", secretName);
            throw new InvalidOperationException($"Failed to check secret existence: {secretName}", ex);
        }
    }

    public string GenerateSecretName(Guid tenantId, Guid connectorId, string fieldName)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty", nameof(tenantId));
        }

        if (connectorId == Guid.Empty)
        {
            throw new ArgumentException("Connector ID cannot be empty", nameof(connectorId));
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("Field name cannot be null or whitespace", nameof(fieldName));
        }

        // Azure Key Vault secret names:
        // - Must be 1-127 characters
        // - Can only contain alphanumeric characters and hyphens
        // - Must start with a letter
        // - Must not end with a hyphen

        // Sanitize field name (remove special characters, convert to lowercase)
        var sanitizedFieldName = Regex.Replace(fieldName, @"[^a-zA-Z0-9-]", "").ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(sanitizedFieldName))
        {
            throw new ArgumentException($"Field name '{fieldName}' contains no valid characters for Key Vault", nameof(fieldName));
        }

        // Format: {prefix}-{tenantId}-{connectorId}-{fieldName}
        // Remove hyphens from GUIDs and truncate if needed
        var tenantIdStr = tenantId.ToString("N"); // 32 chars, no hyphens
        var connectorIdStr = connectorId.ToString("N"); // 32 chars, no hyphens

        var secretName = $"{_settings.SecretNamePrefix}-{tenantIdStr}-{connectorIdStr}-{sanitizedFieldName}";

        // Ensure it doesn't exceed 127 characters
        if (secretName.Length > 127)
        {
            _logger.LogWarning(
                "Generated secret name exceeds 127 characters. Truncating field name. Original: {SecretName}",
                secretName);

            // Truncate field name to fit within 127 character limit
            var maxFieldNameLength = 127 - _settings.SecretNamePrefix.Length - tenantIdStr.Length - connectorIdStr.Length - 3; // 3 hyphens
            sanitizedFieldName = sanitizedFieldName.Substring(0, Math.Max(1, maxFieldNameLength));
            secretName = $"{_settings.SecretNamePrefix}-{tenantIdStr}-{connectorIdStr}-{sanitizedFieldName}";
        }

        _logger.LogDebug("Generated secret name: {SecretName} for tenant {TenantId}, connector {ConnectorId}, field {FieldName}",
            secretName, tenantId, connectorId, fieldName);

        return secretName;
    }
}
