# Product Overview

Multi-tenant ETL Platform - A modern Vue 3 web application for managing Extract, Transform, Load (ETL) pipelines across multiple tenants with secure data isolation.

## Core Features

- **Real-time Dashboard**: Monitor pipeline status, connector health, and recent executions with interactive cards
- **ETL Pipeline Management**: Visual field mapping editor, pipeline wizard, scheduling, and automated execution
- **Multi-source Connectors**: Databases (SQL Server, PostgreSQL, MySQL), Files (CSV, Excel, JSON), REST APIs
- **Transformation Engine**: 6 built-in templates + custom JavaScript/C# scripting with syntax highlighting
- **Multi-tenant Architecture**: Secure tenant isolation, tenant switching, automatic personal workspace creation
- **Secure Authentication**: OAuth 2.0 Authorization Code Flow with PKCE for secure SPA authentication
- **Role-based Access Control**: SuperAdmin, Admin, and User roles with permission-based authorization
- **Execution Monitoring**: Detailed logs, timeline view, progress tracking, and execution history
- **Schema Management**: Auto-detection, manual definition, versioning, and change tracking
- **API Key Management**: Generate and manage API keys for programmatic access
- **AI Assistant**: Context-aware chatbot powered by Google Gemini AI for user guidance
- **Internationalization**: Full i18n support with 6 languages (en, es, fr, de, it, pt)
- **Metadata Service**: Centralized configuration management with automatic translation of dropdown options
- **Theme Support**: Light and dark modes with Material Design components

## User Roles

- **SuperAdmin**: System-wide administration, tenant management, user management across all tenants
- **Admin**: Tenant-level administration, user management within tenant, full pipeline operations
- **User**: Access to pipelines, connectors, transformations, and executions within their tenant

## Key Workflows

1. **Connector Setup**: Configure connections to databases, file systems, or APIs with schema auto-detection or manual definition
2. **Transformation Creation**: Define data transformations using built-in templates (Filter, Map, Trim, Case Convert, Substring, Replace) or custom JavaScript/C# scripts with syntax highlighting
3. **Pipeline Configuration**: Connect source → transformations → destination with visual field mapping, validation, and pipeline wizard
4. **Execution & Monitoring**: Run pipelines manually or on schedule, monitor progress with real-time logs, timeline view, and execution history

## Supported Connector Types

- **Databases**: SQL Server, PostgreSQL, MySQL (with connection testing and schema auto-detection)
- **Files**: CSV (PapaParse), Excel/XLSX (XLSX library), JSON (with file preview and validation)
- **APIs**: REST APIs with configurable endpoints, authentication, and headers

## Transformation Types

1. **Filter**: Filter rows based on field values with operators (equals, contains, greater than, less than, starts with, ends with, etc.)
2. **Map**: Transform field values using key-value mappings with default values
3. **Trim**: Remove leading/trailing whitespace from text fields
4. **Case Convert**: Convert text to uppercase, lowercase, title case, or camelCase
5. **Substring**: Extract portions of text from fields with start/end positions
6. **Replace**: Find and replace text or patterns (with regex support)
7. **Script**: Custom JavaScript or C# transformation logic with Prism.js syntax highlighting

## Internationalization

- **Supported Languages**: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt)
- **Translation Coverage**: All UI components, validation messages, error messages, metadata labels
- **AI Chatbot**: Responds in user's selected language
- **Language Persistence**: User preference saved in localStorage
- **Easy Extension**: Add new languages by creating JSON files in `src/locales/`
