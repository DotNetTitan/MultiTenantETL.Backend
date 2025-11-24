---
inclusion: always
---

# Product Overview

MultiTenant ETL is a production-ready, secure multi-tenant ASP.NET Core Web API designed for ETL (Extract, Transform, Load) operations with complete tenant isolation.

## Core Features

- **Multi-tenant Architecture**: Complete tenant isolation with per-user tenant switching, automatic personal workspace creation on registration
- **OAuth 2.0 & OpenID Connect**: Powered by OpenIddict 7.2.0 with Authorization Code + PKCE (recommended for SPAs) and Password Grant flows
- **Authorization System**: Role-based (SuperAdmin, Admin, User) and permission-based authorization with custom handlers
- **User Management**: Full CRUD operations, email confirmation, password reset, account activation/deactivation
- **Tenant Management**: Tenant CRUD, user-tenant relationships, role assignment within tenants
- **Token Management**: Refresh token rotation, revocation on password change/logout, short-lived access tokens (15 min)
- **Email Integration**: Azure Communication Services for welcome emails, confirmations, password resets

## Target Use Case

The platform enables organizations to manage ETL pipelines, connectors, and data transformations in a multi-tenant environment where each tenant's data is completely isolated from others. Users can belong to multiple tenants and switch between them seamlessly.

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
