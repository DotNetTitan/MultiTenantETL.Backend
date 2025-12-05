# MultiTenant ETL Documentation

Welcome to the MultiTenant ETL documentation. This documentation is organized into the following sections:

## 📚 Documentation Structure

### [Guides](./guides/)
User-facing setup and development guides for getting started with MultiTenant ETL.

- [Azure Deployment Guide](./guides/AZURE-DEPLOYMENT-GUIDE.md) - Deploy to Azure with production-ready architecture
- [Local PostgreSQL Setup](./guides/SETUP-LOCAL-POSTGRES.md) - Configure local PostgreSQL instead of Docker
- [Test Storage Servers](./guides/TEST_STORAGE_SERVERS.md) - Set up FTP, SFTP, and S3 test servers
- [OAuth Authorization Code Flow with PKCE](./guides/authorization-code-flow-pkce.md) - Implement secure OAuth authentication

### [Architecture](./architecture/)
Technical architecture documentation for understanding the system design.

- [Execution Engine](./architecture/EXECUTION_ENGINE_IMPLEMENTATION.md) - Pipeline execution engine design and implementation
- [Transformation Engine](./architecture/PHASE3_TRANSFORMATION_ENGINE.md) - Data transformation processing architecture
- [Transformation Architecture](./architecture/TRANSFORMATION-ARCHITECTURE-FINAL.md) - Final transformation system design
- [Data Reader/Writer Factories](./architecture/DATA_READER_WRITER_FACTORIES.md) - Factory pattern for connectors
- [Connectors](./architecture/CONNECTORS.md) - Connector implementation summary
- [Audit Logging](./architecture/AUDIT_LOGGING.md) - Audit logging system architecture

### [Authentication](./auth/)
Complete authentication and authorization documentation.

- [Authentication Guide](./auth/authentication-guide.md) - Overview and quick start
- [Clean Architecture](./auth/0-clean-architecture.md) - Project structure and layers
- [Setup](./auth/1-setup.md) - Installation and configuration
- [Email Service](./auth/2-email-service.md) - Azure Communication Services integration
- [Controllers](./auth/3-controllers.md) - Authentication and account endpoints
- [Roles & Claims](./auth/4-roles-claims.md) - Role-based access control
- [Security](./auth/5-security.md) - Security features and hardening
- [Authorization](./auth/6-authorization.md) - Permission-based authorization

### [Development Notes](./development/)
Historical implementation notes and phase completion summaries for reference.

- Phase completion summaries and implementation details
- Refactoring documentation
- Feature implementation notes

### [Postman Collections](./postman/)
API testing collections for Postman.

- [Audit Logs API Collection](./postman/Audit_Logs_API.postman_collection.json)

## 🏠 Main README

For an overview of the project, features, and getting started instructions, see the [main README](../README.md).
