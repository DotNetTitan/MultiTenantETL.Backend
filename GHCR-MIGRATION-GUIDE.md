# GitHub Container Registry Migration Guide

This guide explains how to complete the migration from Azure Container Registry to GitHub Container Registry (GHCR).

## What Changed

- **Removed**: Azure Container Registry (ACR) resources from Bicep infrastructure
- **Updated**: Azure Pipelines to build and push images to GHCR instead of ACR
- **Cost Savings**: GHCR is free for public images and has generous free tier for private images

## Prerequisites

1. A GitHub account with access to your repository
2. GitHub Personal Access Token (PAT) with `write:packages` permission
3. Azure DevOps project with your pipelines

## Setup Steps

### 1. Create GitHub Personal Access Token

1. Go to GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Click "Generate new token (classic)"
3. Give it a name like "Azure DevOps GHCR Access"
4. Select scopes:
   - `write:packages` (allows uploading container images)
   - `read:packages` (allows pulling container images)
   - `delete:packages` (optional, for cleanup)
5. Click "Generate token" and copy the token (you won't see it again!)

### 2. Configure Azure DevOps Service Connection

1. In Azure DevOps, go to Project Settings → Service connections
2. Click "New service connection"
3. Select "Docker Registry"
4. Choose "Others" as registry type
5. Fill in:
   - **Docker Registry**: `https://ghcr.io`
   - **Docker ID**: Your GitHub username
   - **Docker Password**: Paste the GitHub PAT from step 1
   - **Service connection name**: `github-container-registry`
6. Click "Save"

### 3. Update Pipeline Variables

In both `mtetl-dev-vars` and `mtetl-beta-vars` variable groups, add:

- **GITHUB_REPOSITORY**: Your GitHub repo in format `username/repo-name` (e.g., `johndoe/multi-tenant-etl`)

### 4. Make Images Public (Optional but Recommended for Free Tier)

After your first pipeline run:

1. Go to your GitHub profile → Packages
2. Find your `api` and `worker` packages
3. Click on each package → Package settings
4. Scroll down and click "Change visibility" → "Public"

This ensures completely free hosting with no storage limits.

### 5. Deploy Infrastructure

Run your Azure Pipeline or manually deploy:

```bash
az deployment group create \
  --resource-group multi-tenant-etl-dev-rg \
  --template-file infra/main.bicep \
  --parameters @infra/parameters.dev.bicepparam
```

The ACR resource will be removed on the next deployment.

### 6. Run Your Pipeline

Commit and push your changes. The pipeline will:
1. Build your .NET solution
2. Deploy infrastructure (without ACR)
3. Build Docker images and push to GHCR
4. Deploy Container Apps with GHCR images

## Container Image URLs

Your images will be available at:
- API: `ghcr.io/YOUR_USERNAME/YOUR_REPO/api:latest`
- Worker: `ghcr.io/YOUR_USERNAME/YOUR_REPO/worker:latest`

## Pulling Images from GHCR

For public images, no authentication is needed. For private images:

```bash
echo $GITHUB_PAT | docker login ghcr.io -u YOUR_USERNAME --password-stdin
docker pull ghcr.io/YOUR_USERNAME/YOUR_REPO/api:latest
```

## Cost Comparison

### Before (ACR Standard)
- Base cost: ~$20/month
- Storage: Additional charges
- Data transfer: Additional charges

### After (GHCR)
- Public images: **$0/month** (unlimited)
- Private images: **$0/month** (up to 500MB storage, 1GB transfer)
- Over limits: $0.25/GB storage, $0.50/GB transfer

## Cleanup Old ACR (After Successful Migration)

Once you've verified everything works with GHCR:

```bash
# Delete ACR for dev environment
az acr delete --name multitenantetldevcr --resource-group multi-tenant-etl-dev-rg

# Delete ACR for beta environment
az acr delete --name multitenantetlbetacr --resource-group multi-tenant-etl-beta-rg
```

## Troubleshooting

### Pipeline fails with "unauthorized: authentication required"
- Verify your GitHub PAT has `write:packages` permission
- Check the service connection credentials in Azure DevOps
- Ensure the PAT hasn't expired

### Container App fails to pull image
- For private images, you may need to add registry credentials to Container Apps
- Consider making images public for simpler setup

### Image not found on GHCR
- Check the GITHUB_REPOSITORY variable is set correctly
- Verify the pipeline completed the "Build & Push Images" stage successfully
- Check your GitHub profile → Packages to see uploaded images

## Support

For issues with:
- GHCR: Check [GitHub Packages documentation](https://docs.github.com/en/packages)
- Azure Pipelines: Check [Azure DevOps documentation](https://docs.microsoft.com/en-us/azure/devops/pipelines/)
