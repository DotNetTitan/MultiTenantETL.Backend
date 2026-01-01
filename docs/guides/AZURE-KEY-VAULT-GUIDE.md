# Azure Key Vault Integration Guide

This guide explains how to configure and use Azure Key Vault for managing secrets in the MultiTenant ETL application.

## Overview

Azure Key Vault has been integrated into both the API and Worker projects to securely manage sensitive configuration values such as:

- Database connection strings
- Azure Communication Services credentials
- Encryption keys and salts
- OAuth client secrets
- Message broker credentials (RabbitMQ/Azure Service Bus)
- Admin seeding passwords

## Key Features

- **Flexible Authentication**: Supports Managed Identity (recommended for production), Azure CLI (for local development), and Service Principal
- **Automatic Secret Rotation**: Secrets are reloaded every 12 hours to support rotation
- **Environment-Aware**: Disabled by default for local development, easily enabled for production
- **Fallback Support**: If Key Vault is unreachable, the application continues with local configuration

## Configuration

### Local Development (Key Vault Disabled)

By default, Azure Key Vault is disabled for local development. The application uses user secrets and appsettings.json:

```json
{
  "AzureKeyVault": {
    "Enabled": false,
    "KeyVaultName": ""
  }
}
```

Continue using user secrets for local development:

```bash
cd src/MultiTenantETL.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "your-connection-string"
dotnet user-secrets set "Encryption:Key" "your-base64-encoded-key"
dotnet user-secrets set "AzureCommunicationServices:ConnectionString" "your-acs-connection"
```

### Production (Key Vault Enabled with Managed Identity)

The recommended approach for Azure deployments uses Managed Identity:

#### 1. Create Azure Key Vault

```bash
# Create a resource group
az group create --name rg-multitenant-etl --location eastus

# Create Key Vault
az keyvault create \
  --name kv-multitenant-etl-prod \
  --resource-group rg-multitenant-etl \
  --location eastus
```

#### 2. Store Secrets in Key Vault

Azure Key Vault uses hyphens in secret names, which are automatically converted to colons in configuration:

```bash
# Database connection string
az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "ConnectionStrings--DefaultConnection" \
  --value "Host=yourdb.postgres.database.azure.com;Port=5432;Database=MultiTenantETL;Username=admin;Password=secure-password;SslMode=Require"

# Encryption settings
az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "Encryption--Key" \
  --value "$(openssl rand -base64 32)"

az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "Encryption--Salt" \
  --value "$(openssl rand -base64 32)"

# Azure Communication Services
az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "AzureCommunicationServices--ConnectionString" \
  --value "endpoint=https://youracs.communication.azure.com/;accesskey=your-key"

az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "AzureCommunicationServices--SenderEmailAddress" \
  --value "noreply@yourdomain.com"

# OAuth seeding secrets
az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "Seeding--AdminPassword" \
  --value "your-secure-admin-password"

az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "Seeding--OAuthClientSecret" \
  --value "your-oauth-client-secret"

# Azure Service Bus (if using)
az keyvault secret set \
  --vault-name kv-multitenant-etl-prod \
  --name "ServiceBus--ConnectionString" \
  --value "Endpoint=sb://yournamespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key"
```

#### 3. Enable Managed Identity for Your App Service / Container

**For Azure App Service:**

```bash
# Enable system-assigned managed identity
az webapp identity assign \
  --name app-multitenant-etl-api \
  --resource-group rg-multitenant-etl

# Get the principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name app-multitenant-etl-api \
  --resource-group rg-multitenant-etl \
  --query principalId -o tsv)

# Grant access to Key Vault
az keyvault set-policy \
  --name kv-multitenant-etl-prod \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

**For Azure Container Apps / AKS:**

```bash
# Create user-assigned managed identity
az identity create \
  --name id-multitenant-etl \
  --resource-group rg-multitenant-etl

# Get identity details
IDENTITY_ID=$(az identity show \
  --name id-multitenant-etl \
  --resource-group rg-multitenant-etl \
  --query principalId -o tsv)

# Grant access to Key Vault
az keyvault set-policy \
  --name kv-multitenant-etl-prod \
  --object-id $IDENTITY_ID \
  --secret-permissions get list
```

#### 4. Configure Application Settings

Set the following in your App Service configuration or as environment variables:

```bash
# For App Service
az webapp config appsettings set \
  --name app-multitenant-etl-api \
  --resource-group rg-multitenant-etl \
  --settings \
    AzureKeyVault__Enabled=true \
    AzureKeyVault__KeyVaultName=kv-multitenant-etl-prod

# Repeat for Worker
az webapp config appsettings set \
  --name app-multitenant-etl-worker \
  --resource-group rg-multitenant-etl \
  --settings \
    AzureKeyVault__Enabled=true \
    AzureKeyVault__KeyVaultName=kv-multitenant-etl-prod
```

### Alternative: Service Principal Authentication

If you can't use Managed Identity, configure a Service Principal:

#### 1. Create Service Principal

```bash
# Create service principal
az ad sp create-for-rbac \
  --name sp-multitenant-etl \
  --role reader \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-multitenant-etl

# Output will include:
# - appId (Client ID)
# - password (Client Secret)
# - tenant (Tenant ID)
```

#### 2. Grant Key Vault Access

```bash
az keyvault set-policy \
  --name kv-multitenant-etl-prod \
  --spn {appId-from-previous-step} \
  --secret-permissions get list
```

#### 3. Configure Application

```bash
az webapp config appsettings set \
  --name app-multitenant-etl-api \
  --resource-group rg-multitenant-etl \
  --settings \
    AzureKeyVault__Enabled=true \
    AzureKeyVault__KeyVaultName=kv-multitenant-etl-prod \
    AzureKeyVault__TenantId={tenant-id} \
    AzureKeyVault__ClientId={app-id} \
    AzureKeyVault__ClientSecret={client-secret}
```

> **Note**: Store the ClientSecret in Key Vault itself for bootstrap scenarios or use Managed Identity instead.

## Local Development with Azure Key Vault

If you want to test Key Vault integration locally:

### Option 1: Using Azure CLI Authentication

1. Login to Azure CLI:
   ```bash
   az login
   ```

2. Update appsettings.Development.json:
   ```json
   {
     "AzureKeyVault": {
       "Enabled": true,
       "KeyVaultName": "kv-multitenant-etl-dev"
     }
   }
   ```

3. Run the application - it will use your Azure CLI credentials

### Option 2: Using Service Principal

1. Create a development Service Principal (as shown above)

2. Create appsettings.Development.json or use user secrets:
   ```bash
   dotnet user-secrets set "AzureKeyVault:Enabled" "true"
   dotnet user-secrets set "AzureKeyVault:KeyVaultName" "kv-multitenant-etl-dev"
   dotnet user-secrets set "AzureKeyVault:TenantId" "your-tenant-id"
   dotnet user-secrets set "AzureKeyVault:ClientId" "your-client-id"
   dotnet user-secrets set "AzureKeyVault:ClientSecret" "your-client-secret"
   ```

## Environment Variables

You can also configure Key Vault using environment variables:

```bash
export AzureKeyVault__Enabled=true
export AzureKeyVault__KeyVaultName=kv-multitenant-etl-prod

# Optional for Service Principal auth
export AzureKeyVault__TenantId=your-tenant-id
export AzureKeyVault__ClientId=your-client-id
export AzureKeyVault__ClientSecret=your-client-secret
```

## Secret Naming Convention

Azure Key Vault secret names use hyphens (`--`) which are automatically converted to colons (`:`) in .NET configuration:

| Configuration Key | Key Vault Secret Name |
|------------------|----------------------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings--DefaultConnection` |
| `Encryption:Key` | `Encryption--Key` |
| `AzureCommunicationServices:ConnectionString` | `AzureCommunicationServices--ConnectionString` |
| `Seeding:AdminPassword` | `Seeding--AdminPassword` |

## Secret Rotation

The application automatically reloads secrets from Key Vault every 12 hours. To rotate a secret:

1. Update the secret in Key Vault:
   ```bash
   az keyvault secret set \
     --vault-name kv-multitenant-etl-prod \
     --name "Encryption--Key" \
     --value "new-value"
   ```

2. Wait up to 12 hours for automatic reload, or restart the application for immediate effect

## Troubleshooting

### Key Vault is not being used

1. Verify `AzureKeyVault:Enabled` is set to `true`
2. Check application logs for Key Vault configuration messages
3. Ensure `KeyVaultName` or `KeyVaultUri` is configured

### Authentication failures

1. **Managed Identity**: Verify the identity is enabled and has Key Vault access policies
2. **Service Principal**: Verify TenantId, ClientId, and ClientSecret are correct
3. **Local Development**: Ensure you're logged in with `az login`

### Secrets not loading

1. Verify secret names use hyphens (`--`) instead of colons
2. Check Key Vault access policies include "get" and "list" permissions
3. Review application logs for specific error messages

### Application fails to start

If Key Vault is unreachable, the application will log a warning and continue with local configuration. Check:

1. Network connectivity to Key Vault
2. Firewall rules on Key Vault
3. Valid authentication credentials

## Security Best Practices

1. **Use Managed Identity** in production instead of Service Principals
2. **Limit Key Vault access** to only "get" and "list" secret permissions
3. **Enable Key Vault firewall** and restrict access to specific virtual networks
4. **Enable Key Vault soft delete** to protect against accidental deletion
5. **Audit Key Vault access** using Azure Monitor
6. **Rotate secrets regularly** using the built-in reload mechanism
7. **Use separate Key Vaults** for different environments (dev, staging, prod)

## References

- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Use Key Vault from App Service](https://docs.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [Managed Identity Overview](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
