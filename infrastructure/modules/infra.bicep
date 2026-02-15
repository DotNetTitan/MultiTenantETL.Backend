// MultiTenantETL Infrastructure Module
// Deploys all Azure resources within a resource group

// =============================================================================
// Parameters
// =============================================================================

param location string
param tags object
param environmentName string
param acrName string
param keyVaultName string
param appServicePlanName string
param apiAppServiceName string
param workerContainerAppName string
param serviceBusNamespaceName string
param logAnalyticsName string
param appInsightsName string
param containerAppEnvName string
param aspnetcoreEnvironment string

@secure()
param supabaseConnectionString string
param frontendUrl string
@secure()
param serviceBusConnectionStringParam string
@secure()
param etlEncryptionCertBase64 string
@secure()
param etlSigningCertBase64 string
@secure()
param certPassword string
@secure()
param encryptionKey string
@secure()
param encryptionSalt string

// =============================================================================
// Log Analytics Workspace
// =============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: environmentName == 'prod' ? 90 : 30
  }
}

// =============================================================================
// Application Insights
// =============================================================================

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

// =============================================================================
// Key Vault
// =============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enabledForDeployment: true
    enabledForTemplateDeployment: true
  }
}

// Store certificates in Key Vault
resource encryptionCertSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(etlEncryptionCertBase64)) {
  parent: keyVault
  name: 'etl-encryption-cert'
  properties: {
    value: etlEncryptionCertBase64
    contentType: 'application/x-pkcs12'
  }
}

resource signingCertSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(etlSigningCertBase64)) {
  parent: keyVault
  name: 'etl-signing-cert'
  properties: {
    value: etlSigningCertBase64
    contentType: 'application/x-pkcs12'
  }
}

resource certPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(certPassword)) {
  parent: keyVault
  name: 'etl-cert-password'
  properties: {
    value: certPassword
  }
}

resource encryptionKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(encryptionKey)) {
  parent: keyVault
  name: 'encryption-key'
  properties: {
    value: encryptionKey
  }
}

resource encryptionSaltSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(encryptionSalt)) {
  parent: keyVault
  name: 'encryption-salt'
  properties: {
    value: encryptionSalt
  }
}

// =============================================================================
// Container Registry (shared across environments, only created for prod)
// =============================================================================

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = if (environmentName == 'prod') {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// =============================================================================
// Service Bus
// =============================================================================

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

resource executionQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'pipeline-executions'
  properties: {
    maxDeliveryCount: 5
    lockDuration: 'PT5M'
    defaultMessageTimeToLive: 'P1D'
    deadLetteringOnMessageExpiration: true
  }
}

resource cancellationQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'pipeline-cancellations'
  properties: {
    maxDeliveryCount: 3
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    deadLetteringOnMessageExpiration: true
  }
}

// Get Service Bus connection string
var serviceBusEndpoint = '${serviceBus.id}/AuthorizationRules/RootManageSharedAccessKey'

// =============================================================================
// App Service Plan (for API)
// =============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true // Linux
  }
}

// =============================================================================
// App Service (API)
// =============================================================================

resource apiAppService 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppServiceName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: environmentName == 'prod' ? true : false
      healthCheckPath: '/health'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: aspnetcoreEnvironment }
        { name: 'ConnectionStrings__DefaultConnection', value: supabaseConnectionString }
        { name: 'AzureKeyVault__VaultUri', value: keyVault.properties.vaultUri }
        { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
        { name: 'AzureKeyVault__SecretNamePrefix', value: 'connector' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsights.properties.ConnectionString }
        { name: 'AppSettings__FrontendUrl', value: frontendUrl }
        { name: 'Cors__AllowedOrigins__0', value: frontendUrl }
        { name: 'Messaging__Provider', value: 'ServiceBus' }
      ]
    }
  }
}

// Grant API managed identity access to Key Vault
resource apiKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiAppService.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: apiAppService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// Container App Environment (for Worker)
// =============================================================================

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppEnvName
  location: location
  tags: tags
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

// =============================================================================
// Container App (Worker)
// =============================================================================

resource workerContainerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: workerContainerAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: '${acrName}.azurecr.io'
          // ACR admin credentials — will be configured after ACR is created
          passwordSecretRef: 'acr-password'
          username: acrName
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: 'REPLACE_AFTER_ACR_CREATION'
        }
        {
          name: 'supabase-connection'
          value: supabaseConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: '${acrName}.azurecr.io/multitenant-etl-worker:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'DOTNET_ENVIRONMENT', value: aspnetcoreEnvironment }
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'supabase-connection' }
            { name: 'AzureKeyVault__VaultUri', value: keyVault.properties.vaultUri }
            { name: 'AzureKeyVault__UseKeyVault', value: 'true' }
            { name: 'AzureKeyVault__SecretNamePrefix', value: 'connector' }
            { name: 'ApplicationInsights__ConnectionString', value: appInsights.properties.ConnectionString }
            { name: 'AppSettings__FrontendUrl', value: frontendUrl }
            { name: 'Messaging__Provider', value: 'ServiceBus' }
          ]
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 1 : 0
        maxReplicas: environmentName == 'prod' ? 3 : 1
      }
    }
  }
}

// Grant Worker managed identity access to Key Vault
resource workerKeyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, workerContainerApp.id, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: workerContainerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// Outputs
// =============================================================================

output apiAppServiceUrl string = 'https://${apiAppService.properties.defaultHostName}'
output workerContainerAppFqdn string = workerContainerApp.properties.configuration.ingress != null ? workerContainerApp.properties.latestRevisionFqdn : 'no-ingress'
output keyVaultUri string = keyVault.properties.vaultUri
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output acrLoginServer string = environmentName == 'prod' ? acr.properties.loginServer : '${acrName}.azurecr.io'
output serviceBusConnectionString string = listKeys(serviceBusEndpoint, '2022-10-01-preview').primaryConnectionString
