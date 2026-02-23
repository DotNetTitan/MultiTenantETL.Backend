# Deployment Readiness Review

Date: 2026-02-23

## Scope

This review focused on deployment-critical signals in the repository:

- CI/CD pipeline configuration
- Environment configuration and secret placeholders
- Infrastructure deployment/migration scripts
- Error-handling behavior that can affect production auth flows
- Local validation feasibility in this execution environment

## What was validated

1. Reviewed CI/CD pipeline (`azure-pipelines.yml`) and deployment template (`.azure-pipelines/deploy-steps.yml`).
2. Reviewed API/Worker runtime configuration files for placeholder values and production defaults.
3. Fixed one production-impacting API behavior in token grant handling (see below).
4. Attempted to run .NET CLI validation commands, but the environment does not have `dotnet` installed.

## Change made during review

### ✅ OAuth token endpoint now returns protocol-compliant error for unsupported grant types

- **File:** `src/MultiTenantETL.API/Controllers/AuthenticationController.cs`
- **Before:** threw `NotImplementedException` for unsupported grant type (can surface as HTTP 500).
- **After:** returns OpenIddict-compatible `unsupported_grant_type` error via `Forbid(...)`.

Why this matters:

- Prevents avoidable 500 responses for client-side grant misuse.
- Aligns endpoint behavior with OAuth/OpenID Connect error semantics.
- Improves API reliability and observability in production.

## Remaining pre-deployment checklist

### 1) Pipeline promotion flow is not fully enabled (High)

In `azure-pipelines.yml`, only **Dev** deployment stage is active. Beta/Staging/Prod stages are currently commented out.

- Action: Uncomment and validate promotion stages when ready.
- Action: Confirm approval gates are configured on `staging` and `prod` environments.

### 2) Required variable groups and secrets must be present (High)

The deployment pipeline expects multiple environment variables/secrets (connection strings, certs, encryption values, OAuth secret, admin password, etc.).

- Action: Verify variable groups exist for each target environment and all required keys are populated.
- Action: Rotate any bootstrap secrets before first production deploy.

### 3) Runtime placeholder values must never ship as-is (High)

`appsettings.json` in API and Worker includes placeholder secret values (`REPLACE_WITH_*`).

- Action: Ensure production uses secure environment variables / Key Vault-backed config.
- Action: Add/verify startup validation that fails fast if placeholders are detected in non-development environments.

### 4) Migration execution path must be verified in target infra (Medium)

Deploy template runs EF migrations during deployment.

- Action: Confirm migration principal has required DB permissions.
- Action: Validate rollback strategy if migration fails mid-release.

### 5) Container App prerequisites are still manual (Medium)

Template notes one-time manual identity/registry binding steps.

- Action: Confirm these steps are completed for each environment before rollout.
- Action: Consider codifying these prerequisites in IaC to reduce drift.

### 6) Release validation in this environment was limited (Medium)

`dotnet` is not installed in this execution environment, so compile/test/migration commands could not be run here.

- Action: Run full build + unit/integration tests + migration dry-run in CI or a .NET 8-ready environment before go-live.

## Recommended go/no-go gate (minimum)

Before deployment, require all of the following:

- [ ] Build + tests pass in CI
- [ ] Docker images build and scan pass
- [ ] DB migration dry-run succeeds on target-like environment
- [ ] Required variable groups are complete for target environment
- [ ] Beta/Staging/Prod stages enabled (if applicable to this rollout)
- [ ] Health checks (`/health`, `/alive`) verified post-deploy
- [ ] Rollback plan tested (previous image + DB strategy)

## Commands attempted during this review

- `dotnet --info` → failed (`dotnet: command not found`) in current environment.

