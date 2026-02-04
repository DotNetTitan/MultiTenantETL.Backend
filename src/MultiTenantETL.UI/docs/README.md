# Documentation

Welcome to the Multi-Tenant ETL Platform documentation. This folder contains comprehensive documentation for developers, API consumers, and system administrators.

## Quick Links

- [Main README](../README.md) - Project overview, installation, and quick start
- [Security Policy](../SECURITY.md) - Security policies and vulnerability reporting

## Documentation Structure

### 📚 Guides

Developer guides and how-to documentation:

| Document | Description |
|----------|-------------|
| [OAuth PKCE](guides/oauth-pkce.md) | OAuth 2.0 Authorization Code Flow with PKCE implementation |
| [Configuration Constants](guides/configuration-constants.md) | Backend-driven configuration system |
| [Quick Start](guides/quick-start.md) | Quick start for using app constants and roles |
| [Troubleshooting](guides/troubleshooting.md) | Common issues and solutions |
| [OAuth Implementation Summary](guides/oauth-implementation-summary.md) | Overview of the OAuth implementation |

### 🔌 API Reference

Complete REST API specifications:

| Document | Description |
|----------|-------------|
| [API Index](api/README.md) | API overview and conventions |
| [Connectors API](api/connectors-api.md) | Data source and destination management |
| [Pipelines API](api/pipelines-api.md) | ETL pipeline configuration |
| [Transformations API](api/transformations-api.md) | Data transformation logic |
| [Executions API](api/executions-api.md) | Pipeline execution tracking |
| [Schedules API](api/schedules-api.md) | Pipeline scheduling |
| [Dashboard API](api/dashboard-api.md) | Dashboard statistics |
| [Metadata API](api/metadata-api.md) | Centralized configuration |
| [Database Implementation](api/database-implementation.md) | Database schema and implementation |

### ⚙️ Backend Development

Backend implementation documentation:

| Document | Description |
|----------|-------------|
| [Backend Overview](backend/README.md) | Backend documentation index and status |
| [Implementation Gaps](backend/backend-implementation-gaps.md) | Gap analysis and requirements |
| [Authentication Guide](backend/auth/authentication-guide.md) | Complete auth/authz guide (index) |
| [Authorization Matrix](backend/authorization-matrix.md) | Permission-based access control |
| [Background Jobs](backend/background-jobs.md) | Hangfire job processing |
| [Real-time SignalR](backend/realtime-signalr.md) | WebSocket implementation |

#### Authentication Modules

Detailed authentication implementation (each file < 500 lines):

| Document | Description |
|----------|-------------|
| [Clean Architecture](backend/auth/0-clean-architecture.md) | Project structure and layers |
| [Setup & Configuration](backend/auth/1-setup.md) | Installation and database setup |
| [Email Service](backend/auth/2-email-service.md) | Azure Communication Services integration |
| [Controllers](backend/auth/3-controllers.md) | Auth and account controllers |
| [Roles & Claims](backend/auth/4-roles-claims.md) | Role-based access and JWT claims |
| [Security Features](backend/auth/5-security.md) | Rate limiting, CORS, headers |
| [Authorization](backend/auth/6-authorization.md) | Permission handlers and policies |

### 🔌 Connectors

Connector-specific documentation:

| Document | Description |
|----------|-------------|
| [Frontend Integration](connectors/frontend-integration.md) | Connector frontend integration guide |

### ✨ Features

Feature-specific documentation:

| Document | Description |
|----------|-------------|
| [Dynamic Token Generation](features/dynamic-token-generation.md) | API connector token management |

## Contributing to Documentation

When adding new documentation:

1. **Choose the right folder:**
   - `guides/` - How-to guides and tutorials
   - `api/` - API specifications
   - `backend/` - Backend implementation details
   - `features/` - Feature documentation
   - `connectors/` - Connector-specific docs

2. **Use consistent naming:**
   - Use lowercase with hyphens (e.g., `my-new-guide.md`)
   - Be descriptive but concise

3. **Update this index:**
   - Add new documents to the appropriate table above

4. **Follow the style:**
   - Use clear headings and sections
   - Include code examples where helpful
   - Add links to related documentation
