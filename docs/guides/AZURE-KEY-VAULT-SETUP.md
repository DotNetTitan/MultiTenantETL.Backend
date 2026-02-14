# Azure Key Vault Setup Guide

This guide explains how to configure Azure Key Vault for secure connector credential storage.

## Overview

All sensitive connector credentials (passwords, API keys, connection strings, etc.) are stored in Azure Key Vault. The application automatically:
- **Creates secrets** when you create/update connectors
- **Resolves references** like `"keyvault:connector-abc123-password"` to actual values
- **Deletes secrets** when you delete connectors

## Prerequisites

- Azure subscription
- Azure CLI installed (`az`) or Azure PowerShell
- Contributor access to create Azure resources

## 1. Create Azure Key Vault

### Using Azure CLI

```bash
# Login to Azure
az login

# Set variables
RESOURCE_GROUP="rg-multitenant-etl"
LOCATION="eastus"
KEY_VAULT_NAME="kv-multitenant-etl"  # Must be globally unique

# Create resource group (if not exists)
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create Key Vault
az keyvault create \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-rbac-authorization true
```

### Using PowerShell

```powershell
# Login to Azure
Connect-AzAccount

# Set variables
$ResourceGroup = "rg-multitenant-etl"
$Location = "eastus"
$KeyVaultName = "kv-multitenant-etl"  # Must be globally unique

# Create resource group (if not exists)
New-AzResourceGroup -Name $ResourceGroup -Location $Location

# Create Key Vault
New-AzKeyVault `
  -Name $KeyVaultName `
  -ResourceGroupName $ResourceGroup `
  -Location $Location `
  -EnableRbacAuthorization
```

## 2. Configure Access Permissions

The application uses **Azure Managed Identity** in production and **developer credentials** locally.

### For Local Development (Your User Account)

```bash
# Get your user principal ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)

# Grant Key Vault Secrets Officer role (create, read, delete secrets)
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee $USER_ID \
  --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME"
```

**PowerShell:**
```powershell
# Get your user ID
$UserId = (Get-AzADUser -SignedIn).Id

# Grant role
New-AzRoleAssignment `
  -ObjectId $UserId `
  -RoleDefinitionName "Key Vault Secrets Officer" `
  -Scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
```

### For Production (Managed Identity)

```bash
# Create managed identity for App Service
az webapp identity assign --name your-app-name --resource-group $RESOURCE_GROUP

# Get the managed identity principal ID
PRINCIPAL_ID=$(az webapp identity show --name your-app-name --resource-group $RESOURCE_GROUP --query principalId -o tsv)

# Grant Key Vault access
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee $PRINCIPAL_ID \
  --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME"
```

## 3. Configure Application Settings

### Local Development - User Secrets

```bash
# Navigate to API project
cd src/MultiTenantETL.API

# Set Key Vault URI
dotnet user-secrets set "AzureKeyVault:VaultUri" "https://kv-multitenant-etl.vault.azure.net/"

# Navigate to Worker project
cd ../MultiTenantETL.Worker

# Set Key Vault URI for worker
dotnet user-secrets set "AzureKeyVault:VaultUri" "https://kv-multitenant-etl.vault.azure.net/"
```

### Production - Environment Variables

Set these environment variables in your Azure App Service or container:

```bash
AzureKeyVault__VaultUri=https://kv-multitenant-etl.vault.azure.net/
AzureKeyVault__UseKeyVault=true
AzureKeyVault__SecretNamePrefix=connector
```

**Azure App Service Configuration:**
```bash
az webapp config appsettings set \
  --name your-app-name \
  --resource-group $RESOURCE_GROUP \
  --settings \
    AzureKeyVault__VaultUri="https://kv-multitenant-etl.vault.azure.net/" \
    AzureKeyVault__UseKeyVault="true" \
    AzureKeyVault__SecretNamePrefix="connector"
```

## 4. Authentication Methods

The application uses `DefaultAzureCredential` which tries authentication in this order:

1. **Environment variables** (service principal)
2. **Managed Identity** (in Azure)
3. **Visual Studio** credentials
4. **Azure CLI** credentials
5. **Azure PowerShell** credentials
6. **Interactive browser** (last resort)

### Local Development

Ensure you're logged in:
```bash
# Azure CLI
az login

# Or Azure PowerShell
Connect-AzAccount
```

### Production (Recommended)

Use **Managed Identity** - no credentials needed in code or configuration.

## 5. Verify Configuration

### Test Access Locally

```bash
# Set a test secret
az keyvault secret set --vault-name $KEY_VAULT_NAME --name "test-secret" --value "test-value"

# Retrieve it
az keyvault secret show --vault-name $KEY_VAULT_NAME --name "test-secret" --query value -o tsv

# Delete test secret
az keyvault secret delete --vault-name $KEY_VAULT_NAME --name "test-secret"
az keyvault secret purge --vault-name $KEY_VAULT_NAME --name "test-secret"
```

### Run the Application

```bash
# Start API with Aspire (includes all services)
cd src/MultiTenantETL.AppHost
dotnet run
```

Create a connector via the API - secrets will automatically be stored in Key Vault.

## 6. Secret Naming Convention

Secrets are automatically named using this pattern:

```
{prefix}-{tenantId}-{connectorId}-{fieldName}
```

**Example:**
```
connector-a1b2c3d4e5f6-g7h8i9j0k1l2-password
connector-a1b2c3d4e5f6-g7h8i9j0k1l2-apikey
```

- **Prefix:** From `AzureKeyVault:SecretNamePrefix` (default: "connector")
- **TenantId:** Tenant GUID (no hyphens)
- **ConnectorId:** Connector GUID (no hyphens)
- **FieldName:** Lowercased field name (password, apikey, etc.)

## 7. Sensitive Fields Detection

The following fields are automatically stored in Key Vault (case-insensitive):

- `password`
- `apiKey`
- `secret`
- `accessKey`
- `secretKey`
- `connectionString`
- `privateKey`

**Example Connector Config:**

When you POST this to create a connector:
```json
{
  "name": "Production PostgreSQL",
  "type": "PostgreSQL",
  "config": {
    "host": "prod-db.postgres.database.azure.com",
    "port": 5432,
    "database": "myapp",
    "username": "admin",
    "password": "MySecretPassword123!"
  }
}
```

The saved ConfigJson will be:
```json
{
  "host": "prod-db.postgres.database.azure.com",
  "port": 5432,
  "database": "myapp",
  "username": "admin",
  "password": "keyvault:connector-tenantid-connectorid-password"
}
```

The actual password `"MySecretPassword123!"` is stored securely in Key Vault.

## 8. Troubleshooting

### "Forbidden" / 403 Errors

**Problem:** Application can't access Key Vault.

**Solutions:**
1. Verify role assignment: `az role assignment list --scope "/subscriptions/.../Microsoft.KeyVault/vaults/$KEY_VAULT_NAME"`
2. Check you're logged in: `az account show`
3. Wait 5-10 minutes for role assignments to propagate

### "Secret not found" Errors

**Problem:** Key Vault reference exists but secret was deleted.

**Solutions:**
1. Check deleted secrets: `az keyvault secret list-deleted --vault-name $KEY_VAULT_NAME`
2. Recover: `az keyvault secret recover --vault-name $KEY_VAULT_NAME --name secret-name`
3. Or purge and recreate connector

### Authentication Issues Locally

**Problem:** Can't authenticate to Azure.

**Solutions:**
1. Run `az login` or `Connect-AzAccount`
2. Check subscription: `az account show`
3. Switch subscription: `az account set --subscription "Subscription Name"`

## 9. Best Practices

### Security
- ✅ Use **Managed Identity** in production (no credentials in config)
- ✅ Enable **Azure Key Vault firewall** to restrict network access
- ✅ Enable **Key Vault diagnostic logs** for audit trail
- ✅ Use **separate Key Vaults** for dev/staging/production

### Maintenance
- 🔄 Set **soft-delete retention** to 90 days (default)
- 🔄 Enable **purge protection** in production
- 🔄 Monitor **Key Vault metrics** (request count, availability)
- 🔄 Implement **secret rotation policies** for long-lived connectors

### Naming
- 📝 Use descriptive Key Vault names: `kv-{project}-{environment}`
- 📝 Keep secret prefix consistent: `connector` (easier to query)
- 📝 Document any custom naming conventions

## 10. Configuration Reference

### appsettings.json
```json
{
  "AzureKeyVault": {
    "VaultUri": "https://your-vault.vault.azure.net/",
    "UseKeyVault": true,
    "SecretNamePrefix": "connector"
  }
}
```

### Environment Variables (Production)
```bash
AzureKeyVault__VaultUri=https://your-vault.vault.azure.net/
AzureKeyVault__UseKeyVault=true
AzureKeyVault__SecretNamePrefix=connector
```

### User Secrets (Local Development)
```bash
dotnet user-secrets set "AzureKeyVault:VaultUri" "https://your-vault.vault.azure.net/"
```

## 11. Cost Considerations

**Azure Key Vault Pricing (Standard tier):**
- Operations: $0.03 per 10,000 transactions
- Secret storage: Minimal (charged per renewal)

**Typical ETL usage:**
- Create connector: 5-10 operations (store secrets)
- Execute pipeline: 5-10 operations (retrieve secrets)
- Delete connector: 5-10 operations (delete secrets)

**Estimate:** ~$5-10/month for most workloads.

## Additional Resources

- [Azure Key Vault Documentation](https://learn.microsoft.com/azure/key-vault/)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)
- [Managed Identity Overview](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [RBAC for Key Vault](https://learn.microsoft.com/azure/key-vault/general/rbac-guide)

---

**Need help?** Check the logs - Key Vault operations are logged with detailed error messages.
