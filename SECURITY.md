# Security Policy

## Supported Versions

As this project is currently in active development, we provide security updates for the latest version available on the main branch. Once we release versioned releases, this table will be updated to reflect our version support policy.

## Reporting a Vulnerability

If you discover a security vulnerability, please report it to us as follows:

- **Do not** create a public issue.
- Email dotnettitan@gmail.com.
- Include detailed information about the vulnerability.
- We will acknowledge receipt within 48 hours and provide a timeline for fixing.

We appreciate your help in keeping MultiTenant ETL secure!

## Security Considerations

### Data Security
- All data is encrypted in transit using HTTPS/TLS
- Database connections use secure connection strings
- Sensitive configuration is stored in user secrets or environment variables

### Authentication Security
- OAuth 2.0 with PKCE for secure authorization
- BCrypt password hashing
- Account lockout after failed attempts
- Token revocation on password changes

### Multi-Tenant Isolation
- Complete tenant data isolation at database level
- Permission-based access control
- Audit logging for all operations

### ETL Pipeline Security
- Connection validation before pipeline execution
- Secure credential storage for data connectors
- Input sanitization and validation
- Execution monitoring and cancellation capabilities