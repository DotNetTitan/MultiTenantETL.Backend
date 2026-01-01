namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Azure Key Vault integration
/// </summary>
public class AzureKeyVaultSettings
{
    /// <summary>
    /// The name of the Azure Key Vault (e.g., "my-keyvault")
    /// </summary>
    public string? KeyVaultName { get; set; }
    
    /// <summary>
    /// Full URI to the Azure Key Vault (e.g., "https://my-keyvault.vault.azure.net/")
    /// If not provided, will be constructed from KeyVaultName
    /// </summary>
    public string? KeyVaultUri { get; set; }
    
    /// <summary>
    /// Whether Azure Key Vault is enabled. Defaults to false for local development.
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Tenant ID for Azure AD authentication (optional, for service principal auth)
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Client ID for Azure AD authentication (optional, for service principal auth)
    /// </summary>
    public string? ClientId { get; set; }
    
    /// <summary>
    /// Client Secret for Azure AD authentication (optional, for service principal auth)
    /// Note: Using Managed Identity is recommended over service principal in production
    /// </summary>
    public string? ClientSecret { get; set; }
    
    /// <summary>
    /// Gets the full Key Vault URI. If KeyVaultUri is set, returns it.
    /// Otherwise constructs URI from KeyVaultName.
    /// </summary>
    public string? GetKeyVaultUri()
    {
        if (!string.IsNullOrEmpty(KeyVaultUri))
        {
            return KeyVaultUri;
        }
        
        if (!string.IsNullOrEmpty(KeyVaultName))
        {
            return $"https://{KeyVaultName}.vault.azure.net/";
        }
        
        return null;
    }
}
