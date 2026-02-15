// MultiTenantETL Infrastructure - Main Bicep Template
// Deploys: App Service Plan, App Service (API), Container App (Worker),
//          Service Bus, Key Vault, Application Insights, Container Registry
//
// Usage:
//   az deployment sub create --location eastus --template-file infrastructure/main.bicep \
//     --parameters environmentName=dev supabaseConnectionString='...' frontendUrl='...'

targetScope = 'subscription'

// =============================================================================
// Parameters
// =============================================================================

@description('Environment name (dev, beta, staging, prod)')
@allowed(['dev', 'beta', 'staging', 'prod'])
param environmentName string

@description('Azure region for all resources')
param location string = 'eastus'

@description('Supabase PostgreSQL connection string (pooler, port 6543)')
@secure()
param supabaseConnectionString string

@description('Frontend URL for CORS and email links')
param frontendUrl string

@description('Service Bus connection string')
@secure()
param serviceBusConnectionStringParam string = ''

@description('Application Insights connection string (auto-generated if empty)')
param appInsightsConnectionStringParam string = ''

@description('Base64-encoded PFX for ETL Encryption certificate')
@secure()
param etlEncryptionCertBase64 string = ''

@description('Base64-encoded PFX for ETL Signing certificate')
@secure()
param etlSigningCertBase64 string = ''

@description('Password for the PFX certificates')
@secure()
param certPassword string = ''

@description('Encryption key for data at rest')
@secure()
param encryptionKey string = ''

@description('Encryption salt')
@secure()
param encryptionSalt string = ''

// =============================================================================
// Variables
// =============================================================================

var prefix = 'multitenant-etl'
var resourceGroupName = 'rg-${prefix}-${environmentName}'
var tags = {
  project: 'MultiTenantETL'
  environment: environmentName
  managedBy: 'bicep'
}

// Sanitized names (no hyphens for resources that don't allow them)
var acrName = 'multitenantetlacr'
var keyVaultName = '${prefix}-${environmentName}-kv'
var appServicePlanName = '${prefix}-${environmentName}-plan'
var apiAppServiceName = '${prefix}-${environmentName}-api'
var workerContainerAppName = '${prefix}-${environmentName}-worker'
var serviceBusNamespaceName = '${prefix}-${environmentName}-sb'
var logAnalyticsName = '${prefix}-${environmentName}-logs'
var appInsightsName = '${prefix}-${environmentName}-ai'
var containerAppEnvName = '${prefix}-${environmentName}-cae'

// Map environment to ASPNETCORE_ENVIRONMENT
var aspnetEnvironmentMap = {
  dev: 'Development'
  beta: 'Beta'
  staging: 'Staging'
  prod: 'Production'
}
var aspnetcoreEnvironment = aspnetEnvironmentMap[environmentName]

// =============================================================================
// Resource Group
// =============================================================================

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// =============================================================================
// Module: Core Infrastructure
// =============================================================================

module infra 'modules/infra.bicep' = {
  name: 'infra-${environmentName}'
  scope: rg
  params: {
    location: location
    tags: tags
    environmentName: environmentName
    acrName: acrName
    keyVaultName: keyVaultName
    appServicePlanName: appServicePlanName
    apiAppServiceName: apiAppServiceName
    workerContainerAppName: workerContainerAppName
    serviceBusNamespaceName: serviceBusNamespaceName
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
    containerAppEnvName: containerAppEnvName
    aspnetcoreEnvironment: aspnetcoreEnvironment
    supabaseConnectionString: supabaseConnectionString
    frontendUrl: frontendUrl
    serviceBusConnectionStringParam: serviceBusConnectionStringParam
    etlEncryptionCertBase64: etlEncryptionCertBase64
    etlSigningCertBase64: etlSigningCertBase64
    certPassword: certPassword
    encryptionKey: encryptionKey
    encryptionSalt: encryptionSalt
  }
}

// =============================================================================
// Outputs
// =============================================================================

output resourceGroupName string = rg.name
output apiAppServiceUrl string = infra.outputs.apiAppServiceUrl
output workerContainerAppUrl string = infra.outputs.workerContainerAppFqdn
output keyVaultUri string = infra.outputs.keyVaultUri
output appInsightsConnectionString string = infra.outputs.appInsightsConnectionString
output acrLoginServer string = infra.outputs.acrLoginServer
output serviceBusConnectionString string = infra.outputs.serviceBusConnectionString
