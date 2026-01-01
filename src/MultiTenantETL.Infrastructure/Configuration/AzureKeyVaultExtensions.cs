using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MultiTenantETL.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring Azure Key Vault as a configuration provider
/// </summary>
public static class AzureKeyVaultExtensions
{
    /// <summary>
    /// Adds Azure Key Vault as a configuration source if enabled in settings.
    /// Uses DefaultAzureCredential which supports:
    /// - Managed Identity (recommended for Azure deployments)
    /// - Azure CLI (for local development)
    /// - Service Principal (if configured with ClientId/ClientSecret)
    /// - Visual Studio / VS Code authentication
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="hostingEnvironment">The hosting environment</param>
    /// <returns>The configuration builder for chaining</returns>
    public static IConfigurationBuilder AddAzureKeyVaultIfConfigured(
        this IConfigurationBuilder builder,
        IHostEnvironment hostingEnvironment)
    {
        // Build intermediate configuration to read Key Vault settings
        var config = builder.Build();
        var keyVaultSettings = new AzureKeyVaultSettings();
        config.GetSection("AzureKeyVault").Bind(keyVaultSettings);
        
        // Only add Key Vault if explicitly enabled
        if (!keyVaultSettings.Enabled)
        {
            return builder;
        }
        
        var keyVaultUri = keyVaultSettings.GetKeyVaultUri();
        if (string.IsNullOrEmpty(keyVaultUri))
        {
            // Log warning but continue - Key Vault is configured but missing URI
            System.Diagnostics.Trace.TraceWarning(
                "Azure Key Vault is enabled but KeyVaultName or KeyVaultUri is not configured. Skipping Key Vault integration.");
            return builder;
        }
        
        try
        {
            TokenCredential credential = GetAzureCredential(keyVaultSettings, hostingEnvironment);
            
            var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
            
            builder.AddAzureKeyVault(secretClient, new AzureKeyVaultConfigurationOptions
            {
                // Reload secrets every 12 hours to support secret rotation
                ReloadInterval = TimeSpan.FromHours(12)
            });
            
            // Success - log via trace
            System.Diagnostics.Trace.TraceInformation(
                $"Azure Key Vault configured successfully: {keyVaultUri} (Environment: {hostingEnvironment.EnvironmentName})");
        }
        catch (Exception ex)
        {
            // Log error but don't fail application startup
            // This allows the app to run with local configuration if Key Vault is unreachable
            System.Diagnostics.Trace.TraceWarning(
                $"Failed to configure Azure Key Vault: {ex.Message}. Continuing with local configuration.");
        }
        
        return builder;
    }
    
    /// <summary>
    /// Gets the appropriate Azure credential based on settings and environment
    /// </summary>
    private static TokenCredential GetAzureCredential(
        AzureKeyVaultSettings settings,
        IHostEnvironment hostingEnvironment)
    {
        // If service principal credentials are provided, use them
        if (!string.IsNullOrEmpty(settings.TenantId) &&
            !string.IsNullOrEmpty(settings.ClientId) &&
            !string.IsNullOrEmpty(settings.ClientSecret))
        {
            System.Diagnostics.Trace.TraceInformation(
                "Using Service Principal authentication for Azure Key Vault");
            return new ClientSecretCredential(
                settings.TenantId,
                settings.ClientId,
                settings.ClientSecret);
        }
        
        // For production, prefer Managed Identity
        // For development, fall back to Azure CLI, Visual Studio, etc.
        System.Diagnostics.Trace.TraceInformation(
            $"Using DefaultAzureCredential for Azure Key Vault (Environment: {hostingEnvironment.EnvironmentName})");
        
        var defaultAzureCredentialOptions = new DefaultAzureCredentialOptions
        {
            // Exclude shared token cache to avoid issues in containerized environments
            ExcludeSharedTokenCacheCredential = true,
            
            // In development, prefer Azure CLI for local development
            ExcludeAzureCliCredential = false,
            
            // Enable VS/VS Code auth for local development
            ExcludeVisualStudioCredential = false,
            ExcludeVisualStudioCodeCredential = false,
            
            // Enable Managed Identity for Azure deployments (first in chain)
            ExcludeManagedIdentityCredential = false
        };
        
        return new DefaultAzureCredential(defaultAzureCredentialOptions);
    }
}
