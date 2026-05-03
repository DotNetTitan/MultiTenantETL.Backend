namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Azure Key Vault integration.
/// </summary>
public class AzureKeyVaultSettings
{
    public const string SectionName = "AzureKeyVault";

    /// <summary>
    /// The Azure Key Vault URI (e.g., https://your-vault.vault.azure.net/)
    /// </summary>
    public required string VaultUri { get; set; }

    /// <summary>
    /// Whether to use Azure Key Vault for storing connector credentials.
    /// When false, falls back to local encryption (not recommended for production).
    /// </summary>
    public bool UseKeyVault { get; set; } = true;

    /// <summary>
    /// Prefix for secret names in Key Vault (default: "connector")
    /// </summary>
    public string SecretNamePrefix { get; set; } = "connector";

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    public void Validate()
    {
        if (UseKeyVault)
        {
            if (string.IsNullOrWhiteSpace(VaultUri))
            {
                throw new InvalidOperationException(
                    "Azure Key Vault URI is required when UseKeyVault is enabled. " +
                    "Set 'AzureKeyVault:VaultUri' in configuration.");
            }

            if (!Uri.TryCreate(VaultUri, UriKind.Absolute, out var uri) || 
                !uri.Host.EndsWith(".vault.azure.net", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Invalid Azure Key Vault URI: '{VaultUri}'. " +
                    "Expected format: https://your-vault.vault.azure.net/");
            }
        }
    }
}
