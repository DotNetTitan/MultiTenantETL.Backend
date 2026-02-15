<#
.SYNOPSIS
    Applies Entity Framework Core migrations to the target Supabase PostgreSQL database.

.DESCRIPTION
    This script runs EF Core migrations against the specified database using the direct
    Supabase connection (port 5432). It is designed to be used as a pre-deployment step
    in the Azure DevOps pipeline.

    IMPORTANT: Use the direct connection string (port 5432), NOT the pooler (port 6543).
    EF Core migrations require DDL operations that are not compatible with connection pooling.

.PARAMETER ConnectionString
    The direct Supabase PostgreSQL connection string.
    Format: Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>;SSL Mode=Require;Trust Server Certificate=true

.PARAMETER Environment
    The target environment (Development, Beta, Staging, Production).

.EXAMPLE
    .\apply-migrations.ps1 -ConnectionString "Host=db.xxx.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=xxx;SSL Mode=Require;Trust Server Certificate=true" -Environment "Production"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Development", "Beta", "Staging", "Production")]
    [string]$Environment = "Production"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MultiTenantETL - Database Migration" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure dotnet-ef is installed
Write-Host "Checking for dotnet-ef tool..." -ForegroundColor Yellow
$efVersion = dotnet ef --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installing dotnet-ef tool..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef --version 8.*
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install dotnet-ef tool"
        exit 1
    }
}
Write-Host "Using dotnet-ef version: $efVersion" -ForegroundColor Green

# Determine paths
$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptRoot
$infraProject = Join-Path $repoRoot "src\MultiTenantETL.Infrastructure\MultiTenantETL.Infrastructure.csproj"
$startupProject = Join-Path $repoRoot "src\MultiTenantETL.API\MultiTenantETL.API.csproj"

Write-Host "Infrastructure project: $infraProject" -ForegroundColor Gray
Write-Host "Startup project: $startupProject" -ForegroundColor Gray
Write-Host ""

# List pending migrations
Write-Host "Checking pending migrations..." -ForegroundColor Yellow
dotnet ef migrations list `
    --project $infraProject `
    --startup-project $startupProject `
    --no-build `
    --connection $ConnectionString 2>&1 | ForEach-Object { Write-Host "  $_" }

Write-Host ""

# Apply migrations
Write-Host "Applying migrations..." -ForegroundColor Yellow
dotnet ef database update `
    --project $infraProject `
    --startup-project $startupProject `
    --connection $ConnectionString `
    --verbose

if ($LASTEXITCODE -ne 0) {
    Write-Error "Migration failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Migrations applied successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
