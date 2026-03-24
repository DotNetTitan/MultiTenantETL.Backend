// ============================================================================
// MultiTenant ETL – Azure Infrastructure
// Deploys: Container Apps (API + Worker), Storage Queue, Key Vault, SWA
//
// Usage:
//   az deployment group create \
//     --resource-group multi-tenant-etl-dev-rg \
//     --template-file infra/main.bicep \
//     --parameters @infra/parameters.dev.bicepparam
//
// First-time setup (once per environment, before second deploy):
//   1. az storage account show-connection-string \
//        --name <storageAccountName> \
//        --resource-group <rg> \
//        --query connectionString -o tsv
//   2. az keyvault secret set \
//        --vault-name <keyVaultName> \
//        --name StorageQueueConnection \
//        --value "<connection-string>"
//
// To verify role definition IDs:
//   az role definition list --name "Key Vault Secrets User" --query "[].name" -o tsv
//   az role definition list --name "Storage Queue Data Contributor" --query "[].name" -o tsv
// ============================================================================

@description('Environment name (dev or beta)')
@allowed(['dev', 'beta'])
param environmentName string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Azure region for Static Web App (must be a supported SWA region)')
param staticWebAppLocation string = 'eastus2'

// ── Computed resource names ──────────────────────────────────────────────────
var prefix = 'mtetl'
var logAnalyticsName = '${prefix}-${environmentName}-law'
var managedIdentityName = '${prefix}-${environmentName}-mi'
var storageAccountName = '${replace(prefix, '-', '')}${replace(environmentName, '-', '')}stor'
var keyVaultName = '${prefix}-${environmentName}-kv'
var containerAppsEnvName = '${prefix}-${environmentName}-cae'
var apiAppName = '${prefix}-api-${environmentName}'
var workerAppName = '${prefix}-worker-${environmentName}'
var staticWebAppName = '${prefix}-web-${environmentName}'

// ── Key Vault DNS suffix (avoids hardcoded vault.azure.net) ─────────────────
var kvDnsSuffix = environment().suffixes.keyvaultDns  // .vault.azure.net in AzureCloud
var kvUri = 'https://${keyVaultName}${kvDnsSuffix}/'

// ── Built-in role definition IDs ────────────────────────────────────────────
var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
)
var storageQueueDataContributorRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
)

// ── Log Analytics Workspace ──────────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── User-Assigned Managed Identity ──────────────────────────────────────────
// Shared by API and Worker Container Apps for:
//   • Reading secrets from Key Vault (Key Vault Secrets User)
//   • Publishing/consuming Storage Queue messages (Storage Queue Data Contributor)
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// ── Azure Storage Account (Standard, Hot, LRS) ──────────────────────────────
// Used for message queues (pipeline-executions, pipeline-cancellations).
// Cost: ~$0.015/GB/month at-rest + per-operation charges (minimal for light usage)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ── Storage Queue Service + Queues ──────────────────────────────────────────
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

// pipeline-executions queue – matches StorageQueueSettings.ExecutionQueueName
resource execQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'pipeline-executions'
}

// pipeline-cancellations queue – matches StorageQueueSettings.CancellationQueueName
resource cancelQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  parent: queueService
  name: 'pipeline-cancellations'
}

// ── Azure Key Vault (Standard, RBAC) ────────────────────────────────────────
// Used by the app's ISecretStorageService to store connector credentials.
// Also holds the StorageQueueConnection secret used by the KEDA scaler.
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ── Role: Key Vault Secrets User → Managed Identity ─────────────────────────
resource kvSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Role: Storage Queue Data Contributor → Managed Identity ─────────────────
resource storageQueueDataAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentity.id, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageQueueDataContributorRoleId
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Container Apps Environment ───────────────────────────────────────────────
resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppsEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── API Container App ────────────────────────────────────────────────────────
// External HTTP ingress on port 8080 (matches Dockerfile ASPNETCORE_URLS).
// Scales to zero when idle; HTTP scaler wakes it on incoming requests.
// Initial image is an MCR placeholder – the pipeline replaces it with GHCR image on first deploy.
resource apiContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: apiAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          // Placeholder image – replaced by the pipeline with GHCR image on every run.
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'AzureKeyVault__VaultUri', value: kvUri }
            { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
            { name: 'Messaging__Provider', value: 'StorageQueue' }
            { name: 'Messaging__StorageAccountName', value: storageAccountName }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
        rules: [
          {
            // Wake the API on incoming HTTP requests
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

// ── Worker Container App ─────────────────────────────────────────────────────
// No HTTP ingress – background Storage Queue consumer only.
// Worker runs EF Core database migrations on startup.
// Scales to zero when queue is empty; KEDA wakes it when messages arrive.
//
// IMPORTANT: The StorageQueueConnection secret must exist in Key Vault before
// this deploys successfully. See first-time setup instructions at top of file.
resource workerContainerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: workerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
    //  secrets: [
    //   {
          // Connection string for KEDA azure-queue scaler.
          // Pulled from Key Vault using the managed identity.
          // Must be populated manually after first deploy (see top of file).
          // name: 'storage-queue-conn'
          // keyVaultUrl: '${kvUri}secrets/StorageQueueConnection'
          // identity: managedIdentity.id
    //    }
    //  ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          // Placeholder image – replaced by the pipeline with GHCR image on every run.
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'DOTNET_ENVIRONMENT', value: 'Production' }
            { name: 'AzureKeyVault__VaultUri', value: kvUri }
            { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
            { name: 'Messaging__Provider', value: 'StorageQueue' }
            { name: 'Messaging__StorageAccountName', value: storageAccountName }
            { name: 'EmailService__UseStub', value: 'false' }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1 // Worker processes one batch at a time; scale-out requires coordination
        rules: [
          {
            // Wake the worker when messages appear in the executions queue.
            // Authenticates via the storage-queue-conn secret sourced from Key Vault.
            name: 'storage-queue-scaler'
            custom: {
              type: 'azure-queue'
              metadata: {
                queueName: 'pipeline-executions'
                queueLength: '1' // wake on first message
              }
              // auth: [
              //  {
              //    secretRef: 'storage-queue-conn'
              //    triggerParameter: 'connection'
              //  }
              ]
            }
          }
        ]
      }
    }
  }
}

// ── Azure Static Web App (Free tier) ────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    buildProperties: {
      skipGithubActionWorkflowGeneration: true // pipeline-managed; skip auto-generated GH Action
    }
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────
// Use these values to populate your Azure DevOps variable groups after first deploy.

@description('API Container App public URL – set as VITE_API_BASE_URL in the frontend variable group and as AzureCommunicationServices__ApplicationUrl in the backend variable group')
output apiUrl string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'

@description('Static Web App default hostname – set as Cors__AllowedOrigins__0 and AzureCommunicationServices__FrontendUrl in the backend variable group')
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'

@description('Key Vault URI (auto-injected into Container Apps via Bicep; shown here for reference)')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Storage Account name – use this to retrieve the connection string for the one-time Key Vault setup')
output storageAccountName string = storageAccount.name

@description('Storage Queue endpoint – useful for verifying app config')
output storageQueueEndpoint string = storageAccount.properties.primaryEndpoints.queue

@description('Managed Identity resource ID')
output managedIdentityId string = managedIdentity.id