# Audit Logging System

## Overview

The audit logging system provides comprehensive tracking of user actions and system events across the multi-tenant ETL platform. All security-sensitive operations are automatically logged with full context including user, tenant, IP address, and timestamps.

## Features

- **Automatic Logging**: Authentication, user management, and tenant operations are automatically logged
- **Multi-Tenant Support**: Logs are scoped to tenants with proper isolation
- **Rich Context**: Captures user, tenant, IP address, user agent, and metadata
- **Queryable**: Full API for filtering and searching audit logs
- **Non-Blocking**: Audit failures never break application functionality
- **Performance**: Indexed for fast queries on large datasets

## What Gets Logged

### Authentication Events
- Login (success and failures)
- Logout
- Registration
- Password changes
- Password resets
- Email confirmations
- Tenant switching

### User Management
- User creation
- User updates
- User deletion
- User activation/deactivation
- Role assignments
- Tenant membership changes

### Tenant Management
- Tenant creation
- Tenant updates
- Tenant deletion
- User additions to tenants
- User removals from tenants
- Role changes within tenants

### Future: ETL Operations
- Pipeline creation/updates/deletion
- Pipeline executions
- Connector operations
- Transformation changes

## Database Schema

```sql
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY,
    tenant_id UUID NULL,  -- NULL for system-level events
    user_id UUID NULL,    -- NULL for system events
    user_email VARCHAR(256),
    action VARCHAR(100),  -- e.g., "Auth.Login", "User.Created"
    resource_type VARCHAR(50),  -- e.g., "User", "Tenant", "Pipeline"
    resource_id VARCHAR(100),
    description TEXT,
    ip_address VARCHAR(45),
    user_agent TEXT,
    metadata JSONB,  -- Additional context as JSON
    severity VARCHAR(20),  -- Info, Warning, Error
    success BOOLEAN,
    error_message TEXT,
    created_at TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_audit_logs_tenant_id ON audit_logs(tenant_id);
CREATE INDEX idx_audit_logs_user_id ON audit_logs(user_id);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at);
```

## Usage

### In Controllers

```csharp
// Inject IAuditService
private readonly IAuditService _auditService;

// Log an action
await _auditService.LogAsync(
    action: AuditActions.Users.Created,
    resourceType: "User",
    resourceId: user.Id.ToString(),
    description: $"User created: {user.Email}");

// Log authentication event
await _auditService.LogAuthenticationAsync(
    action: AuditActions.Authentication.Login,
    userEmail: user.Email,
    success: true);

// Log with metadata
await _auditService.LogAsync(
    action: AuditActions.Pipelines.Executed,
    resourceType: "Pipeline",
    resourceId: pipelineId.ToString(),
    description: "Pipeline executed successfully",
    metadata: new { 
        rowsProcessed = 1000,
        duration = "5.2s"
    });
```

### Querying Audit Logs

#### API Endpoints

**Get Audit Logs (Admin)**
```http
GET /api/AuditLogs?page=1&pageSize=50&action=User.Created&startDate=2024-01-01
Authorization: Bearer {token}
```

**Get My Audit Logs (Any User)**
```http
GET /api/AuditLogs/my-logs?page=1&pageSize=50
Authorization: Bearer {token}
```

#### Query Parameters
- `userId` - Filter by user ID
- `action` - Filter by action (e.g., "Auth.Login")
- `resourceType` - Filter by resource type (e.g., "User", "Tenant")
- `startDate` - Filter by start date (ISO 8601)
- `endDate` - Filter by end date (ISO 8601)
- `page` - Page number (default: 1)
- `pageSize` - Items per page (default: 50, max: 100)

## Action Constants

All audit actions are defined in `Domain/Constants/AuditActions.cs`:

```csharp
// Authentication
AuditActions.Authentication.Login
AuditActions.Authentication.LoginFailed
AuditActions.Authentication.Logout
AuditActions.Authentication.Register
AuditActions.Authentication.PasswordChanged
AuditActions.Authentication.PasswordReset
AuditActions.Authentication.EmailConfirmed
AuditActions.Authentication.TenantSwitched

// Users
AuditActions.Users.Created
AuditActions.Users.Updated
AuditActions.Users.Deleted
AuditActions.Users.Activated
AuditActions.Users.Deactivated
AuditActions.Users.RoleAssigned
AuditActions.Users.RoleRemoved
AuditActions.Users.AddedToTenant
AuditActions.Users.RemovedFromTenant
AuditActions.Users.TenantRoleUpdated

// Tenants
AuditActions.Tenants.Created
AuditActions.Tenants.Updated
AuditActions.Tenants.Deleted
AuditActions.Tenants.UserAdded
AuditActions.Tenants.UserRemoved
AuditActions.Tenants.UserRoleUpdated

// Pipelines (for future use)
AuditActions.Pipelines.Created
AuditActions.Pipelines.Updated
AuditActions.Pipelines.Deleted
AuditActions.Pipelines.Executed
AuditActions.Pipelines.ExecutionFailed
```

## Access Control

- **SuperAdmin**: Can view all audit logs across all tenants
- **TenantAdmin**: Can view audit logs for their tenant only
- **Users**: Can view their own audit logs via `/my-logs` endpoint

## Best Practices

### When to Log
✅ **DO log:**
- Authentication events (login, logout, password changes)
- Data modifications (create, update, delete)
- Permission changes
- Configuration changes
- Failed operations (for security monitoring)
- Tenant switching

❌ **DON'T log:**
- Read operations (too noisy)
- Health checks
- Internal system operations
- Sensitive data (passwords, tokens)

### What to Include

**Description**: Human-readable summary
```csharp
description: "User john@example.com added to tenant Acme Corp with role Admin"
```

**Metadata**: Structured data for analysis
```csharp
metadata: new {
    oldValue = "User",
    newValue = "Admin",
    changedBy = currentUser.Email
}
```

### Error Handling

The audit service is designed to never break your application:

```csharp
try {
    // Audit logging happens here
} catch (Exception ex) {
    // Logged but never thrown
    _logger.LogError(ex, "Failed to write audit log");
}
```

## Compliance & Retention

### GDPR Considerations
- User email is stored for audit purposes
- Consider anonymizing after user deletion
- Provide data export functionality

### Retention Policy
- Recommended: Keep audit logs for 90 days minimum
- Compliance requirements may require longer (1-7 years)
- Implement automated archival/deletion

### Example Cleanup Query
```sql
-- Delete audit logs older than 90 days
DELETE FROM audit_logs 
WHERE created_at < NOW() - INTERVAL '90 days';
```

## Performance Considerations

- Audit writes are async and non-blocking
- Indexes on tenant_id, user_id, action, created_at
- Consider partitioning by date for large datasets
- Archive old logs to separate storage

## Monitoring

### Key Metrics to Track
- Failed login attempts (security)
- Audit log write failures (system health)
- Unusual activity patterns (anomaly detection)
- Audit log growth rate (capacity planning)

### Example Queries

**Failed Login Attempts (Last 24 Hours)**
```sql
SELECT user_email, COUNT(*) as attempts, MAX(created_at) as last_attempt
FROM audit_logs
WHERE action = 'Auth.LoginFailed'
  AND created_at > NOW() - INTERVAL '24 hours'
GROUP BY user_email
HAVING COUNT(*) > 5
ORDER BY attempts DESC;
```

**Most Active Users**
```sql
SELECT user_email, COUNT(*) as actions
FROM audit_logs
WHERE created_at > NOW() - INTERVAL '7 days'
  AND user_email IS NOT NULL
GROUP BY user_email
ORDER BY actions DESC
LIMIT 10;
```

## Future Enhancements

- [ ] Real-time audit log streaming
- [ ] Anomaly detection and alerts
- [ ] Audit log export (CSV, JSON)
- [ ] Audit log visualization dashboard
- [ ] Integration with SIEM systems
- [ ] Automated compliance reports
