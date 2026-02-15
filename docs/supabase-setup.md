# Supabase PostgreSQL Setup Guide

This guide walks you through setting up Supabase as the PostgreSQL database for MultiTenantETL.

## 1. Create Supabase Projects

Create a **separate Supabase project for each environment**:

| Environment | Suggested Project Name |
|-------------|----------------------|
| Dev | `multitenant-etl-dev` |
| Beta | `multitenant-etl-beta` |
| Staging | `multitenant-etl-staging` |
| Prod | `multitenant-etl-prod` |

### Steps:
1. Go to [https://supabase.com/dashboard](https://supabase.com/dashboard)
2. Click **New Project**
3. Select your organization
4. Enter the project name, set a strong database password, and select **East US (North Virginia)** as the region
5. Click **Create new project**
6. Repeat for each environment

## 2. Get Connection Strings

For each project, navigate to **Settings → Database** and collect two connection strings:

### Direct Connection (for migrations — port 5432)
Used by the `apply-migrations.ps1` script and EF Core CLI. This connects directly to PostgreSQL, which is required for DDL operations (CREATE TABLE, ALTER TABLE, etc.).

```
Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-db-password>;SSL Mode=Require;Trust Server Certificate=true
```

### Pooler Connection (for runtime — port 6543)
Used by the API and Worker at runtime. This connects through Supavisor (Supabase's built-in connection pooler), which is better for application workloads because it manages connection limits efficiently.

```
Host=aws-0-us-east-1.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.<project-ref>;Password=<your-db-password>;SSL Mode=Require;Trust Server Certificate=true
```

> **Important:** The pooler connection uses **transaction mode** by default. This works well with EF Core's normal query patterns. Do not use session mode unless you have specific requirements.

## 3. Configure Azure DevOps Variable Groups

For each environment, create a variable group in Azure DevOps (**Pipelines → Library**):

| Variable Group Name | Example |
|---------------------|---------|
| `multitenant-etl-dev` | Dev environment secrets |
| `multitenant-etl-beta` | Beta environment secrets |
| `multitenant-etl-staging` | Staging environment secrets |
| `multitenant-etl-prod` | Production environment secrets |

Add these variables to each group (mark sensitive values as **secret**):

| Variable | Value | Secret? |
|----------|-------|---------|
| `SupabaseConnectionString` | Pooler connection string (port 6543) | ✅ |
| `SupabaseMigrationConnectionString` | Direct connection string (port 5432) | ✅ |
| `KeyVaultUri` | `https://multitenant-etl-<env>-kv.vault.azure.net/` | ❌ |
| `AppInsightsConnectionString` | From Azure portal after Bicep deployment | ❌ |
| `ServiceBusConnectionString` | From Azure portal after Bicep deployment | ✅ |
| `AcsCommunicationConnectionString` | Azure Communication Services connection string | ✅ |
| `AcsSenderEmail` | Sender email address | ❌ |
| `EncryptionKey` | Generate with `openssl rand -base64 32` | ✅ |
| `EncryptionSalt` | Generate with `openssl rand -base64 32` | ✅ |
| `AdminPassword` | Strong admin password | ✅ |
| `OAuthClientSecret` | OAuth client secret | ✅ |
| `FrontendUrl` | `https://your-frontend-<env>.com` | ❌ |
| `EtlEncryptionCertBase64` | Base64-encoded PFX (see section 5) | ✅ |
| `EtlSigningCertBase64` | Base64-encoded PFX (see section 5) | ✅ |
| `CertPassword` | PFX certificate password | ✅ |

## 4. Apply Initial Migrations

After creating each Supabase project, apply the initial EF Core migrations:

```powershell
# From the repository root
.\infrastructure\apply-migrations.ps1 `
  -ConnectionString "Host=db.<project-ref>.supabase.co;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true" `
  -Environment "Development"
```

## 5. Generate OpenIddict Certificates

Generate the encryption and signing certificates for each environment:

```powershell
# Generate Encryption Certificate
$cert = New-SelfSignedCertificate -Subject "CN=ETL-Encryption" -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec KeyExchange -KeyLength 2048 -NotAfter (Get-Date).AddYears(5)
$pwd = ConvertTo-SecureString -String "YourCertPassword" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ".\etl-encryption.pfx" -Password $pwd
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\etl-encryption.pfx"))
# Copy the output → paste into EtlEncryptionCertBase64 variable

# Generate Signing Certificate
$cert = New-SelfSignedCertificate -Subject "CN=ETL-Signing" -CertStoreLocation "Cert:\CurrentUser\My" -KeyExportPolicy Exportable -KeySpec Signature -KeyLength 2048 -NotAfter (Get-Date).AddYears(5)
Export-PfxCertificate -Cert $cert -FilePath ".\etl-signing.pfx" -Password $pwd
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\etl-signing.pfx"))
# Copy the output → paste into EtlSigningCertBase64 variable
```

> **Important:** Use **different certificates for each environment**. Never share production certificates with dev/beta/staging.

## 6. Supabase Security Recommendations

- **Row Level Security (RLS):** RLS is enabled by default on Supabase tables. Since the application connects as the `postgres` role (which bypasses RLS), this does not affect your EF Core queries. If you add Supabase client-side access later, configure RLS policies.
- **Network restrictions:** In the Supabase dashboard, go to **Settings → Database → Network Restrictions** and whitelist your Azure App Service and Container App outbound IPs for production.
- **Connection limits:** The Supabase Free plan allows 60 direct connections. The pooler expands this significantly. For production, use a paid plan (Pro gives 200 direct connections + unlimited pooled).
- **Backups:** Supabase Pro includes daily backups with 7-day retention. Enable Point-in-Time Recovery for production.
