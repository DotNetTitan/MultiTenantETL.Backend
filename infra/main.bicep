// ============================================================================
// MultiTenant ETL – Azure Infrastructure
// Deploys: ACR, Container Apps (API + Worker), Service Bus, Key Vault, SWA
//
// Usage:
//   az deployment group create \
//     --resource-group multi-tenant-etl-dev-rg \
//     --template-file infra/main.bicep \
//     --parameters @infra/parameters.dev.bicepparam
//
// To verify Service Bus Data Owner role ID:
//   az role definition list --name "Azure Service Bus Data Owner" --query "[].name" -o tsv
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
// ACR names: alphanumeric only, max 50 chars
var acrName = 'multitenantetl${environmentName}cr'
var logAnalyticsName = '${prefix}-${environmentName}-law'
var managedIdentityName = '${prefix}-${environmentName}-mi'
var serviceBusName = 'multi-tenant-etl-${environmentName}-sb-ns'
var keyVaultName = '${prefix}-${environmentName}-kv'
var containerAppsEnvName = '${prefix}-${environmentName}-cae'
var apiAppName = '${prefix}-api-${environmentName}'
var workerAppName = '${prefix}-worker-${environmentName}'
var staticWebAppName = '${prefix}-web-${environmentName}'

// ── Built-in role definition IDs ────────────────────────────────────────────
// Verify with: az role definition list --name "<Role Name>" --query "[].name" -o tsv
var acrPullRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d' // AcrPull
)
var keyVaultSecretsUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
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

// ── Azure Container Registry ─────────────────────────────────────────────────
resource acr 'Microsoft.ContainerRegistry/registries@2022-12-01' = {
  name: acrName
  location: location
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false // ACR pull is via managed identity – no admin credentials
  }
}

// ── User-Assigned Managed Identity ──────────────────────────────────────────
// Shared by API and Worker Container Apps for:
//   • Pulling images from ACR (AcrPull)
//   • Reading secrets from Key Vault (Key Vault Secrets User)
//   • Publishing/consuming Service Bus messages (Azure Service Bus Data Owner)
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// ── Role: AcrPull on ACR ─────────────────────────────────────────────────────
resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, managedIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Azure Service Bus (Standard) ─────────────────────────────────────────────
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    disableLocalAuth: false // connection string auth used by the application
  }
}

// pipeline-executions queue – matches ServiceBusSettings.ExecutionQueueName
resource sbExecQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'pipeline-executions'
  properties: {
    lockDuration: 'PT5M'           // 5 min – matches lock renewal in ServiceBusWorker
    maxDeliveryCount: 5            // matches ServiceBusSettings.MaxRetryAttempts
    deadLetteringOnMessageExpiration: true
    enablePartitioning: false
  }
}

// pipeline-cancellations queue – matches ServiceBusSettings.CancellationQueueName
resource sbCancelQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'pipeline-cancellations'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 3
    deadLetteringOnMessageExpiration: true
    enablePartitioning: false
  }
}


// ── Azure Key Vault (Standard, RBAC) ────────────────────────────────────────
// Used by the app's ISecretStorageService to store connector credentials.
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true  // RBAC-based access (no legacy access policies)
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
// Initial image is an MCR placeholder – the pipeline replaces it on first deploy.
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
      registries: [
        {
          server: acr.properties.loginServer
          identity: managedIdentity.id
        }
      ]
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
          // Placeholder image from public registry – does not require ACR credentials.
          // The deployment pipeline replaces this with the real image on every run.
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'AzureKeyVault__VaultUri', value: 'https://${keyVaultName}.vault.azure.net/' }
            { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
            { name: 'Messaging__Provider', value: 'ServiceBus' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
  dependsOn: [acrPullAssignment]
}

// ── Worker Container App ──────────────────────────────────────────────────────
// No HTTP ingress – background Service Bus consumer only.
// Worker runs EF Core database migrations on startup.
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
      registries: [
        {
          server: acr.properties.loginServer
          identity: managedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          // Placeholder image – replaced by the pipeline on first deploy.
          image: 'mcr.microsoft.com/dotnet/aspnet:8.0'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'DOTNET_ENVIRONMENT', value: 'Production' }
            { name: 'AzureKeyVault__VaultUri', value: 'https://${keyVaultName}.vault.azure.net/' }
            { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
            { name: 'Messaging__Provider', value: 'ServiceBus' }
            { name: 'EmailService__UseStub', value: 'false' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1 // Worker processes one batch at a time; scale-out requires coordination
      }
    }
  }
  dependsOn: [acrPullAssignment]
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

@description('Container Registry login server (e.g., multitenantetldevcr.azurecr.io)')
output acrLoginServer string = acr.properties.loginServer

@description('API Container App public URL – set as VITE_API_BASE_URL in the frontend variable group and as AzureCommunicationServices__ApplicationUrl in the backend variable group')
output apiUrl string = 'https://${apiContainerApp.properties.configuration.ingress.fqdn}'

@description('Static Web App default hostname – set as Cors__AllowedOrigins__0 and AzureCommunicationServices__FrontendUrl in the backend variable group')
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'

@description('Key Vault URI (auto-injected into Container Apps via Bicep; shown here for reference)')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Service Bus namespace name – pipeline uses this to retrieve the connection string automatically')
output serviceBusNamespaceName string = serviceBusNamespace.name

@description('Managed Identity resource ID')
output managedIdentityId string = managedIdentity.id
