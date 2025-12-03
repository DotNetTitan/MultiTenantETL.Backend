---
inclusion: always
---

# Product Overview

MultiTenant ETL is a production-ready, secure multi-tenant ASP.NET Core 8.0 platform designed for ETL (Extract, Transform, Load) operations with complete tenant isolation, scalable pipeline execution, and enterprise-grade security.

## Core Features

### Multi-tenant Architecture
- Complete tenant isolation with per-user tenant switching
- Automatic personal workspace creation on registration
- Users can belong to multiple tenants and switch between them seamlessly
- Role-based access within tenants (SuperAdmin, Admin, User)

### ETL Pipeline Engine
- **Pipeline Management**: Create and configure ETL pipelines with source and destination connectors
- **Field Mappings**: Configure field-to-field mappings with inline transformations
- **Execution Tracking**: Real-time progress tracking with detailed logging
- **Batch Processing**: Memory-efficient streaming with configurable batch sizes (default 1,000 rows)
- **Scheduling Support**: Schedule configuration for automated pipeline runs

### Data Connectors
- **Database Connectors**: SQL Server, PostgreSQL, MySQL with connection pooling
- **File Connectors**: CSV, JSON, JSONL with local, FTP, SFTP, Azure Blob, and S3 storage
- **API Connectors**: REST API with authentication, headers, and pagination support
- **Connection Testing**: Validate connector configurations before pipeline execution
- **Schema Detection**: Auto-detect source schemas for field mapping assistance

### Transformation Engine
- **Filter Transformations**: Include/exclude rows based on conditions (equals, contains, regex, greater than, less than, etc.)
- **Map Transformations**: Rename fields, apply value mappings
- **String Transformations**: Trim, case conversion (upper/lower/title), substring, replace, pad, concatenate
- **Script Transformations**: Custom JavaScript expressions for complex transformation logic
- **Field-Level Processing**: Apply transformations to specific fields within a batch

### Message Broker Integration (RabbitMQ)
- Asynchronous pipeline execution via background workers
- Horizontal scalability with multiple worker instances
- Durable queues with dead letter exchange for failed tasks
- Graceful cancellation via dedicated queue
- Configurable retry with exponential backoff

### OAuth 2.0 & OpenID Connect
- Powered by OpenIddict 7.2.0
- Authorization Code + PKCE (recommended for SPAs) - RFC 7636 compliant
- Password Grant for API testing/machine-to-machine communication
- Refresh token rotation with 7-day lifetime

### Authorization System
- Role-based (SuperAdmin, Admin, User) authorization
- Permission-based authorization with custom handlers
- Tenant resource-based authorization

### User & Tenant Management
- Full user CRUD operations, email confirmation, password reset, account activation/deactivation
- Tenant CRUD, user-tenant relationships, role assignment within tenants

### Email Integration
- Azure Communication Services for welcome emails, confirmations, password resets

## Target Use Case

The platform enables organizations to manage ETL pipelines, connectors, and data transformations in a multi-tenant environment where each tenant's data is completely isolated from others. Typical use cases include:
- Data migration between systems
- Scheduled data synchronization
- Data warehousing and reporting pipelines
- API data aggregation and processing

## User Roles

- **SuperAdmin**: System-wide administration, tenant management, user management across all tenants
- **Admin**: Tenant-level administration, user management within tenant, full pipeline operations
- **User**: Access to pipelines, connectors, transformations, and executions within their tenant

## Security Focus

Security is a primary concern with features including:
- **OAuth 2.0 PKCE**: Proof Key for Code Exchange prevents authorization code interception attacks in SPAs
- **Public Client Support**: No client secret required for frontend applications
- **Single-use Authorization Codes**: Codes can only be exchanged once for tokens
- **State Parameter**: CSRF protection for OAuth flows
- Account lockout (5 failed attempts, 15-minute lockout)
- Rate limiting on authentication endpoints
- CORS configuration for frontend origins
- Security headers middleware
- Password complexity requirements (min 8 chars, uppercase, lowercase, digit, special char)
- Email enumeration prevention
- BCrypt password hashing
- Input sanitization utilities
- Permission-based authorization policies
- Complete tenant data isolation
