# Azure Deployment Guide - MultiTenant ETL Platform

## Overview

This guide covers deploying the MultiTenant ETL platform to Azure with a production-ready architecture optimized for scalability, reliability, and cost-effectiveness.

## Architecture Components

### 1. API Service (ASP.NET Core Web API)
**What it is:** REST API that handles user requests, authentication, and pipeline management  
**What it does:** Creates execution records, publishes messages to RabbitMQ, returns immediately  
**Deployment target:** Azure App Service

### 2. Worker Service (.NET Worker Service)
**What it is:** Background service that processes pipeline executions  
**What it does:** Listens to RabbitMQ, executes pipelines (Extract → Transform → Load), updates database  
**Deployment target:** Azure Container Apps

### 3. RabbitMQ (Message Broker)
**What it is:** Message queue that decouples API from Workers  
**What it does:** Stores execution tasks, distributes work to workers, ensures reliability  
**Deployment target:** CloudAMQP (Managed RabbitMQ)

### 4. PostgreSQL (Database)
**What it is:** Relational database for all application data  
**What it does:** Stores users, tenants, pipelines, executions, logs  
**Deployment target:** Azure Database for PostgreSQL

## Deployment Architecture

```
Internet
   │
   ▼
┌─────────────────────────────────────────────────┐
│  Azure Front Door (Optional)                    │
│  - SSL termination                              │
│  - CDN / Caching                                │
│  - DDoS protection                              │
└────────────┬────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────┐
│  Azure App Service (API)                        │
│  - ASP.NET Core Web API                         │
│  - Auto-scale: 2-10 instances                   │
│  - Health checks enabled                        │
│  - Application Insights monitoring              │
└────────────┬────────────────────────────────────┘
             │ Publishes messages
             ▼
┌─────────────────────────────────────────────────┐
│  CloudAMQP (RabbitMQ)                           │
│  - Managed RabbitMQ service                     │
│  - Durable queues                               │
│  - High availability                            │
│  - Automatic backups                            │
└────────────┬────────────────────────────────────┘
             │ Workers consume messages
             ▼
┌─────────────────────────────────────────────────┐
│  Azure Container Apps (Worker Service)          │
│  - .NET Worker Service in containers            │
│  - KEDA auto-scale: 1-20 instances              │
│  - Scale based on RabbitMQ queue depth          │
│  - Scale to 0 when idle                         │
└────────────┬────────────────────────────────────┘
             │ Read/Write data
             ▼
┌─────────────────────────────────────────────────┐
│  Azure Database for PostgreSQL                  │
│  - Flexible Server                              │
│  - High availability enabled                    │
│  - Automated backups (7-35 days)                │
│  - Private endpoint (VNet integration)          │
└─────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────┐
│  Azure Key Vault                                │
│  - Connection strings                           │
│  - API keys                                     │
│  - Certificates                                 │
└─────────────────────────────────────────────────┘
```


## Component Details

### Azure App Service (API)

**Service Type:** Platform as a Service (PaaS)  
**Pricing Tier:** Standard S1 or Premium P1V2 (production)

**Features:**
- Built-in load balancing
- Auto-scaling based on CPU/memory/HTTP queue
- Deployment slots (blue-green deployments)
- Easy CI/CD integration (GitHub Actions, Azure DevOps)
- Built-in SSL certificates
- Application Insights integration

**Configuration:**
- **Runtime:** .NET 8
- **OS:** Linux (cheaper) or Windows
- **Always On:** Enabled
- **Auto-scale rules:**
  - Scale out when CPU > 70%
  - Scale in when CPU < 30%
  - Min instances: 2 (high availability)
  - Max instances: 10

**Environment Variables:**
```bash
ConnectionStrings__DefaultConnection=@Microsoft.KeyVault(SecretUri=...)
RabbitMq__HostName=<cloudamqp-hostname>
RabbitMq__UserName=<username>
RabbitMq__Password=@Microsoft.KeyVault(SecretUri=...)
```

**Estimated Cost:** $50-150/month (S1 tier, 2-4 instances)

---

### Azure Container Apps (Worker Service)

**Service Type:** Serverless containers  
**Why Container Apps?**
- Perfect for background workers
- Auto-scales based on custom metrics (RabbitMQ queue depth)
- Scales to zero when idle (cost savings)
- No need to manage infrastructure

**Configuration:**
- **Container Image:** Your Worker Docker image from Azure Container Registry
- **CPU:** 0.5-1.0 cores per instance
- **Memory:** 1-2 GB per instance
- **Min replicas:** 1
- **Max replicas:** 20

**KEDA Scaling Rule (RabbitMQ):**
```yaml
scale:
  minReplicas: 1
  maxReplicas: 20
  rules:
  - name: rabbitmq-queue-length
    type: rabbitmq
    metadata:
      queueName: pipeline-executions
      queueLength: "5"  # Scale up when >5 messages in queue
      host: <rabbitmq-connection-string>
```

**How it works:**
- 0-5 messages: 1 worker
- 6-10 messages: 2 workers
- 11-15 messages: 3 workers
- Scales up/down automatically

**Estimated Cost:** $30-200/month (depends on usage, scales to 0 when idle)

---

### CloudAMQP (RabbitMQ)

**Service Type:** Managed RabbitMQ (SaaS)  
**Why CloudAMQP?**
- Zero ops overhead
- Automatic updates and patches
- Built-in monitoring and alerting
- High availability out of the box
- Expert support

**Plans:**
- **Little Lemur:** $19/month - Development/staging
- **Tough Tiger:** $99/month - Production (recommended)
- **Roaring Rabbit:** $299/month - High scale

**Features:**
- Persistent queues
- Automatic backups
- Monitoring dashboard
- Alarms and notifications
- 99.9% SLA (Tough Tiger+)

**Configuration:**
```json
{
  "RabbitMq": {
    "HostName": "<instance>.cloudamqp.com",
    "Port": 5672,
    "UserName": "<username>",
    "Password": "<password>",
    "VirtualHost": "<vhost>",
    "UseSsl": true
  }
}
```

**Alternative:** Azure Service Bus
- More expensive (~$10-500/month)
- Azure-native
- Different API (requires code changes)
- Good if you want to avoid third-party services

**Estimated Cost:** $99/month (Tough Tiger plan)

---

### Azure Database for PostgreSQL

**Service Type:** Managed PostgreSQL (PaaS)  
**Tier:** Flexible Server (recommended)

**Configuration:**
- **Compute:** General Purpose, 2-4 vCores
- **Storage:** 128-256 GB SSD
- **High Availability:** Zone-redundant (recommended for production)
- **Backup retention:** 7-35 days
- **Version:** PostgreSQL 16

**Features:**
- Automatic backups
- Point-in-time restore
- Automatic patching
- Built-in monitoring
- Private endpoint support (VNet integration)

**Connection String:**
```
Host=<server>.postgres.database.azure.com;
Port=5432;
Database=MultiTenantETL;
Username=<admin>@<server>;
Password=<password>;
SslMode=Require;
```

**Security:**
- Store connection string in Azure Key Vault
- Use Managed Identity where possible
- Enable firewall rules (allow Azure services)
- Consider Private Endpoint for production

**Estimated Cost:** $100-300/month (2-4 vCores, HA enabled)

---

### Azure Key Vault

**Service Type:** Secrets management  
**Why Key Vault?**
- Centralized secret storage
- Automatic rotation support
- Audit logging
- Managed Identity integration

**Secrets to Store:**
- Database connection strings
- RabbitMQ credentials
- OAuth client secrets
- Azure Communication Services keys
- Encryption keys

**Access from App Service:**
```csharp
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/DbConnectionString)"
  }
}
```

**Estimated Cost:** $5-10/month


## Deployment Steps

### Prerequisites

1. Azure subscription
2. Azure CLI installed
3. Docker installed (for Worker container)
4. .NET 8 SDK installed

### Step 1: Create Azure Resources

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "<subscription-id>"

# Create resource group
az group create \
  --name rg-multitenant-etl-prod \
  --location eastus

# Create Azure Container Registry (for Worker images)
az acr create \
  --resource-group rg-multitenant-etl-prod \
  --name acrmultitenantETL \
  --sku Basic

# Create PostgreSQL Flexible Server
az postgres flexible-server create \
  --resource-group rg-multitenant-etl-prod \
  --name psql-multitenant-etl-prod \
  --location eastus \
  --admin-user pgadmin \
  --admin-password '<strong-password>' \
  --sku-name Standard_B2s \
  --tier Burstable \
  --storage-size 128 \
  --version 16 \
  --high-availability Enabled

# Create database
az postgres flexible-server db create \
  --resource-group rg-multitenant-etl-prod \
  --server-name psql-multitenant-etl-prod \
  --database-name MultiTenantETL

# Create Key Vault
az keyvault create \
  --resource-group rg-multitenant-etl-prod \
  --name kv-multitenant-etl \
  --location eastus

# Create App Service Plan
az appservice plan create \
  --resource-group rg-multitenant-etl-prod \
  --name asp-multitenant-etl-prod \
  --sku S1 \
  --is-linux

# Create App Service (API)
az webapp create \
  --resource-group rg-multitenant-etl-prod \
  --plan asp-multitenant-etl-prod \
  --name app-multitenant-etl-api \
  --runtime "DOTNETCORE:8.0"

# Create Container Apps Environment
az containerapp env create \
  --resource-group rg-multitenant-etl-prod \
  --name cae-multitenant-etl \
  --location eastus
```

### Step 2: Setup CloudAMQP

1. Go to https://www.cloudamqp.com/
2. Sign up / Login
3. Create new instance:
   - **Name:** multitenant-etl-prod
   - **Plan:** Tough Tiger ($99/month)
   - **Region:** Azure / East US
   - **Tags:** production
4. Note the connection details (AMQP URL)

### Step 3: Store Secrets in Key Vault

```bash
# Database connection string
az keyvault secret set \
  --vault-name kv-multitenant-etl \
  --name DbConnectionString \
  --value "Host=psql-multitenant-etl-prod.postgres.database.azure.com;Port=5432;Database=MultiTenantETL;Username=pgadmin;Password=<password>;SslMode=Require"

# RabbitMQ connection string
az keyvault secret set \
  --vault-name kv-multitenant-etl \
  --name RabbitMqConnectionString \
  --value "amqps://<user>:<password>@<instance>.cloudamqp.com/<vhost>"

# OAuth client secret
az keyvault secret set \
  --vault-name kv-multitenant-etl \
  --name OAuthClientSecret \
  --value "<generate-strong-secret>"

# Admin password
az keyvault secret set \
  --vault-name kv-multitenant-etl \
  --name AdminPassword \
  --value "<generate-strong-password>"
```

### Step 4: Deploy API to App Service

```bash
# Build and publish API
cd src/MultiTenantETL.API
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../api.zip .

# Deploy to App Service
az webapp deployment source config-zip \
  --resource-group rg-multitenant-etl-prod \
  --name app-multitenant-etl-api \
  --src ../api.zip

# Configure App Service settings
az webapp config appsettings set \
  --resource-group rg-multitenant-etl-prod \
  --name app-multitenant-etl-api \
  --settings \
    ConnectionStrings__DefaultConnection="@Microsoft.KeyVault(SecretUri=https://kv-multitenant-etl.vault.azure.net/secrets/DbConnectionString)" \
    RabbitMq__HostName="<cloudamqp-host>" \
    RabbitMq__UserName="<username>" \
    RabbitMq__Password="@Microsoft.KeyVault(SecretUri=https://kv-multitenant-etl.vault.azure.net/secrets/RabbitMqPassword)"

# Enable Managed Identity
az webapp identity assign \
  --resource-group rg-multitenant-etl-prod \
  --name app-multitenant-etl-api

# Grant Key Vault access
az keyvault set-policy \
  --name kv-multitenant-etl \
  --object-id <managed-identity-principal-id> \
  --secret-permissions get list
```

### Step 5: Build and Deploy Worker to Container Apps

```bash
# Build Worker Docker image
cd src/MultiTenantETL.Worker

# Create Dockerfile (if not exists)
cat > Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MultiTenantETL.Worker/MultiTenantETL.Worker.csproj", "MultiTenantETL.Worker/"]
COPY ["MultiTenantETL.Application/MultiTenantETL.Application.csproj", "MultiTenantETL.Application/"]
COPY ["MultiTenantETL.Domain/MultiTenantETL.Domain.csproj", "MultiTenantETL.Domain/"]
COPY ["MultiTenantETL.Infrastructure/MultiTenantETL.Infrastructure.csproj", "MultiTenantETL.Infrastructure/"]
RUN dotnet restore "MultiTenantETL.Worker/MultiTenantETL.Worker.csproj"
COPY . .
WORKDIR "/src/MultiTenantETL.Worker"
RUN dotnet build "MultiTenantETL.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MultiTenantETL.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MultiTenantETL.Worker.dll"]
EOF

# Build and push to ACR
az acr build \
  --registry acrmultitenantETL \
  --image worker:latest \
  --file Dockerfile \
  ../../

# Deploy to Container Apps
az containerapp create \
  --resource-group rg-multitenant-etl-prod \
  --name ca-multitenant-etl-worker \
  --environment cae-multitenant-etl \
  --image acrmultitenantETL.azurecr.io/worker:latest \
  --registry-server acrmultitenantETL.azurecr.io \
  --cpu 1.0 \
  --memory 2.0Gi \
  --min-replicas 1 \
  --max-replicas 20 \
  --env-vars \
    ConnectionStrings__DefaultConnection="<from-keyvault>" \
    RabbitMq__HostName="<cloudamqp-host>" \
    RabbitMq__UserName="<username>" \
    RabbitMq__Password="<password>"
```

### Step 6: Run Database Migrations

```bash
# From your local machine (with connection to Azure PostgreSQL)
cd src/MultiTenantETL.API

# Update connection string in user secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=psql-multitenant-etl-prod.postgres.database.azure.com;Port=5432;Database=MultiTenantETL;Username=pgadmin;Password=<password>;SslMode=Require"

# Run migrations
dotnet ef database update --project ../MultiTenantETL.Infrastructure
```

### Step 7: Configure Auto-Scaling

**App Service (API):**
```bash
# Scale out rule (CPU)
az monitor autoscale create \
  --resource-group rg-multitenant-etl-prod \
  --resource app-multitenant-etl-api \
  --resource-type Microsoft.Web/serverfarms \
  --name autoscale-api \
  --min-count 2 \
  --max-count 10 \
  --count 2

az monitor autoscale rule create \
  --resource-group rg-multitenant-etl-prod \
  --autoscale-name autoscale-api \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 1

az monitor autoscale rule create \
  --resource-group rg-multitenant-etl-prod \
  --autoscale-name autoscale-api \
  --condition "CpuPercentage < 30 avg 5m" \
  --scale in 1
```

**Container Apps (Worker) - KEDA Scaling:**
```yaml
# Create scaling rule YAML
cat > worker-scale.yaml << 'EOF'
scale:
  minReplicas: 1
  maxReplicas: 20
  rules:
  - name: rabbitmq-queue-rule
    type: rabbitmq
    metadata:
      protocol: auto
      queueName: pipeline-executions
      mode: QueueLength
      value: "5"
      host: <rabbitmq-connection-string>
EOF

# Apply scaling rule
az containerapp update \
  --resource-group rg-multitenant-etl-prod \
  --name ca-multitenant-etl-worker \
  --scale-rule-name rabbitmq-queue-rule \
  --scale-rule-type rabbitmq \
  --scale-rule-metadata \
    queueName=pipeline-executions \
    queueLength=5 \
    host=<rabbitmq-connection-string>
```


## Cost Breakdown

### Monthly Costs (Production Environment)

| Component | Service | Tier/Size | Estimated Cost |
|-----------|---------|-----------|----------------|
| API | Azure App Service | S1 (2-4 instances) | $100-200 |
| Worker | Azure Container Apps | 1-20 instances | $50-200 |
| RabbitMQ | CloudAMQP | Tough Tiger | $99 |
| Database | PostgreSQL Flexible | 2-4 vCores, HA | $150-300 |
| Storage | Azure Blob Storage | Standard | $10-30 |
| Key Vault | Azure Key Vault | Standard | $5-10 |
| Monitoring | Application Insights | Basic | $20-50 |
| Networking | VNet, Private Endpoints | - | $10-30 |
| **Total** | | | **$444-919/month** |

### Cost Optimization Tips

1. **Use Reserved Instances** - Save 30-50% on compute
2. **Scale to Zero** - Container Apps can scale to 0 when idle
3. **Right-size resources** - Start small, scale up as needed
4. **Use Azure Hybrid Benefit** - If you have Windows Server licenses
5. **Monitor and optimize** - Use Azure Cost Management


## Monitoring and Observability

### Application Insights

**Setup:**
```bash
# Create Application Insights
az monitor app-insights component create \
  --resource-group rg-multitenant-etl-prod \
  --app ai-multitenant-etl \
  --location eastus \
  --application-type web

# Get instrumentation key
az monitor app-insights component show \
  --resource-group rg-multitenant-etl-prod \
  --app ai-multitenant-etl \
  --query instrumentationKey
```

**Configure in API:**
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "<key>",
    "EnableAdaptiveSampling": true
  }
}
```

**Key Metrics to Monitor:**
- API response times
- Request rates
- Failure rates
- Worker execution times
- RabbitMQ queue depth
- Database connection pool
- Exception rates

### Alerts

**Critical Alerts:**
1. API availability < 99%
2. Worker service down
3. RabbitMQ queue depth > 1000
4. Database CPU > 80%
5. Failed executions > 10%

**Setup Alert:**
```bash
az monitor metrics alert create \
  --resource-group rg-multitenant-etl-prod \
  --name alert-api-availability \
  --description "API availability below 99%" \
  --scopes /subscriptions/<sub-id>/resourceGroups/rg-multitenant-etl-prod/providers/Microsoft.Web/sites/app-multitenant-etl-api \
  --condition "avg Availability < 99" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action <action-group-id>
```


## Security Best Practices

### Network Security

1. **Use Private Endpoints**
   - PostgreSQL: Private endpoint in VNet
   - Key Vault: Private endpoint
   - Container Apps: VNet integration

2. **Firewall Rules**
   - PostgreSQL: Allow only Azure services + specific IPs
   - App Service: IP restrictions if needed

3. **SSL/TLS**
   - API: HTTPS only (App Service provides free SSL)
   - Database: Require SSL connections
   - RabbitMQ: Use amqps:// (SSL)

### Identity and Access

1. **Managed Identity**
   - App Service → Key Vault (no passwords)
   - Container Apps → Key Vault
   - App Service → PostgreSQL (Azure AD auth)

2. **RBAC**
   - Least privilege principle
   - Separate service principals for CI/CD
   - Regular access reviews

3. **Secrets Management**
   - Never commit secrets to Git
   - Use Key Vault for all secrets
   - Rotate secrets regularly

### Application Security

1. **Authentication**
   - OAuth 2.0 + PKCE (already implemented)
   - Short-lived access tokens (15 min)
   - Refresh token rotation

2. **Authorization**
   - Permission-based (already implemented)
   - Tenant isolation (already implemented)

3. **Input Validation**
   - Sanitize all inputs (already implemented)
   - Rate limiting (already implemented)

4. **Audit Logging**
   - Log all sensitive operations (already implemented)
   - Store logs for 365 days


## CI/CD Pipeline

### GitHub Actions Workflow

Create `.github/workflows/deploy-production.yml`:

```yaml
name: Deploy to Production

on:
  push:
    branches: [ main ]

env:
  AZURE_WEBAPP_NAME: app-multitenant-etl-api
  DOTNET_VERSION: '8.0.x'
  ACR_NAME: acrmultitenantETL
  WORKER_IMAGE: worker

jobs:
  build-and-deploy-api:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Build API
      run: |
        cd src/MultiTenantETL.API
        dotnet publish -c Release -o ./publish
    
    - name: Deploy to Azure App Service
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: src/MultiTenantETL.API/publish

  build-and-deploy-worker:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Login to Azure Container Registry
      uses: azure/docker-login@v1
      with:
        login-server: ${{ env.ACR_NAME }}.azurecr.io
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}
    
    - name: Build and push Worker image
      run: |
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.WORKER_IMAGE }}:${{ github.sha }} \
          -f src/MultiTenantETL.Worker/Dockerfile .
        docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.WORKER_IMAGE }}:${{ github.sha }}
    
    - name: Deploy to Container Apps
      uses: azure/container-apps-deploy-action@v1
      with:
        resource-group: rg-multitenant-etl-prod
        container-app-name: ca-multitenant-etl-worker
        image: ${{ env.ACR_NAME }}.azurecr.io/${{ env.WORKER_IMAGE }}:${{ github.sha }}
```


## Alternative: AKS Deployment (Advanced)

For organizations needing maximum control and scale (50+ workers, complex networking):

### Architecture

```
Azure Kubernetes Service (AKS)
├── API Deployment (3 replicas)
├── Worker Deployment (5-50 replicas, HPA enabled)
├── RabbitMQ StatefulSet (3 replicas, HA)
└── Ingress Controller (NGINX)
```

### When to Use AKS

**Use AKS if:**
- Need to run 50+ worker instances
- Complex networking requirements (service mesh, etc.)
- Want full infrastructure control
- Have Kubernetes expertise in-house
- Multi-region deployment
- Need custom node pools (GPU, high-memory, etc.)

**Don't use AKS if:**
- Small team without K8s experience
- Budget-conscious (AKS is more expensive)
- Simple deployment needs
- Want minimal ops overhead

### AKS Setup (High-Level)

```bash
# Create AKS cluster
az aks create \
  --resource-group rg-multitenant-etl-prod \
  --name aks-multitenant-etl \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --enable-managed-identity \
  --enable-addons monitoring \
  --generate-ssh-keys

# Install KEDA for auto-scaling
helm repo add kedacore https://kedacore.github.io/charts
helm install keda kedacore/keda --namespace keda --create-namespace

# Install RabbitMQ
helm repo add bitnami https://charts.bitnami.com/bitnami
helm install rabbitmq bitnami/rabbitmq \
  --set replicaCount=3 \
  --set persistence.enabled=true \
  --set persistence.size=50Gi
```

### Cost Comparison

**Container Apps (Recommended):**
- Simple, managed, auto-scales
- ~$400-900/month total

**AKS (Advanced):**
- Full control, complex setup
- ~$600-1800/month total
- Requires K8s expertise


## Disaster Recovery

### Backup Strategy

**Database (PostgreSQL):**
- Automated backups: 7-35 days retention
- Point-in-time restore
- Geo-redundant backups (optional)

**RabbitMQ (CloudAMQP):**
- Automatic daily backups
- Retained for 7 days
- Can restore to new instance

**Application Code:**
- Git repository (GitHub/Azure DevOps)
- Tagged releases
- Infrastructure as Code (Terraform/Bicep)

### Recovery Procedures

**Database Restore:**
```bash
# Restore to point in time
az postgres flexible-server restore \
  --resource-group rg-multitenant-etl-prod \
  --name psql-multitenant-etl-restored \
  --source-server psql-multitenant-etl-prod \
  --restore-time "2024-01-15T10:30:00Z"
```

**Application Redeployment:**
```bash
# Redeploy API from GitHub
az webapp deployment source config \
  --resource-group rg-multitenant-etl-prod \
  --name app-multitenant-etl-api \
  --repo-url https://github.com/your-org/multitenant-etl \
  --branch main

# Redeploy Worker
az containerapp update \
  --resource-group rg-multitenant-etl-prod \
  --name ca-multitenant-etl-worker \
  --image acrmultitenantETL.azurecr.io/worker:v1.2.3
```

### RTO and RPO

**Recovery Time Objective (RTO):** < 1 hour
- API: 5-10 minutes (redeploy)
- Worker: 5-10 minutes (redeploy)
- Database: 15-30 minutes (restore)
- RabbitMQ: 10-15 minutes (restore)

**Recovery Point Objective (RPO):** < 5 minutes
- Database: Continuous backups
- RabbitMQ: Persistent messages
- Execution state: Stored in database


## Troubleshooting

### Common Issues

**1. Worker not consuming messages**

**Symptoms:** Messages pile up in RabbitMQ, executions stuck in "Queued"

**Diagnosis:**
```bash
# Check worker logs
az containerapp logs show \
  --resource-group rg-multitenant-etl-prod \
  --name ca-multitenant-etl-worker \
  --tail 100

# Check RabbitMQ queue depth
# Login to CloudAMQP dashboard
```

**Solutions:**
- Verify RabbitMQ connection string
- Check worker is running: `az containerapp show`
- Verify network connectivity
- Check for exceptions in logs

---

**2. API slow response times**

**Symptoms:** High latency, timeouts

**Diagnosis:**
```bash
# Check Application Insights
# Look for slow database queries
# Check App Service metrics (CPU, memory)
```

**Solutions:**
- Scale out App Service instances
- Optimize database queries
- Add database indexes
- Enable response caching

---

**3. Database connection pool exhausted**

**Symptoms:** "Connection pool exhausted" errors

**Diagnosis:**
```bash
# Check active connections
SELECT count(*) FROM pg_stat_activity;
```

**Solutions:**
- Increase max pool size in connection string
- Scale up database (more vCores)
- Fix connection leaks in code
- Use connection pooling properly

---

**4. High RabbitMQ queue depth**

**Symptoms:** Messages not being processed fast enough

**Diagnosis:**
- Check CloudAMQP dashboard
- Monitor worker count

**Solutions:**
- Increase max worker replicas
- Optimize pipeline execution time
- Increase worker CPU/memory
- Check for slow transformations


## Production Checklist

### Pre-Deployment

- [ ] All secrets stored in Key Vault
- [ ] Managed Identity configured for all services
- [ ] Database migrations tested
- [ ] SSL/TLS enabled everywhere
- [ ] Firewall rules configured
- [ ] Auto-scaling rules configured
- [ ] Monitoring and alerts set up
- [ ] Backup strategy documented
- [ ] Disaster recovery plan tested
- [ ] CI/CD pipeline working
- [ ] Load testing completed
- [ ] Security scan passed
- [ ] Documentation updated

### Post-Deployment

- [ ] Verify API is accessible
- [ ] Verify worker is consuming messages
- [ ] Run smoke tests
- [ ] Check Application Insights for errors
- [ ] Verify database connectivity
- [ ] Test pipeline execution end-to-end
- [ ] Verify auto-scaling works
- [ ] Check all alerts are firing correctly
- [ ] Document any issues encountered
- [ ] Update runbook

### Ongoing Maintenance

- [ ] Monitor costs weekly
- [ ] Review logs daily
- [ ] Check alerts daily
- [ ] Update dependencies monthly
- [ ] Review security quarterly
- [ ] Test disaster recovery quarterly
- [ ] Optimize performance quarterly
- [ ] Review and update documentation


## Summary

### Recommended Architecture

**For most production deployments:**

1. **API** → Azure App Service (S1 tier, 2-10 instances)
2. **Worker** → Azure Container Apps (KEDA auto-scaling, 1-20 instances)
3. **RabbitMQ** → CloudAMQP (Tough Tiger plan)
4. **Database** → Azure Database for PostgreSQL (Flexible Server, HA enabled)
5. **Secrets** → Azure Key Vault
6. **Monitoring** → Application Insights

**Total Cost:** ~$450-900/month

**Benefits:**
- Fully managed services (minimal ops)
- Auto-scales based on load
- High availability built-in
- Easy to deploy and maintain
- Production-ready security

### Next Steps

1. **Review this guide** and adjust for your specific needs
2. **Create Azure resources** using the provided scripts
3. **Deploy API and Worker** using CI/CD pipeline
4. **Run database migrations**
5. **Configure monitoring and alerts**
6. **Test end-to-end** with sample pipelines
7. **Go live!**

### Support Resources

- **Azure Documentation:** https://docs.microsoft.com/azure
- **CloudAMQP Support:** https://www.cloudamqp.com/support.html
- **Application Insights:** https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview
- **Container Apps:** https://docs.microsoft.com/azure/container-apps/

---

**Document Version:** 1.0  
**Last Updated:** 2024-11-29  
**Status:** Production Ready
