using 'main.bicep'

// Deployment: az deployment sub create --location eastus --template-file infrastructure/main.bicep --parameters infrastructure/main.bicepparam

param environmentName = 'dev'
param location = 'eastus'
param frontendUrl = 'https://your-frontend-url.com'

// Secrets — pass via command line or Azure DevOps pipeline:
//   --parameters supabaseConnectionString='Host=...' encryptionKey='...' encryptionSalt='...'
