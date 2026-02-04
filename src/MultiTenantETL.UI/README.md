# Multi-tenant ETL Platform

[![CI](https://github.com/DotNetTitan/MultiTenantETL.Vue/workflows/Build%20Vue/badge.svg)](https://github.com/DotNetTitan/MultiTenantETL.Vue/actions)

A modern, responsive web application for managing ETL (Extract, Transform, Load) pipelines across multiple tenants. Built with Vue 3 and Vite.

## Features

- 📊 **Real-time Dashboard**: Monitor pipeline status, connector health, and recent executions with interactive cards
- 🔄 **ETL Pipeline Management**: Visual field mapping editor, pipeline wizard, scheduling, and automated execution
- 🔌 **Multi-source Connectors**: Databases (SQL Server, PostgreSQL, MySQL), Files (CSV, Excel, JSON), REST APIs
- 🔍 **Transformation Engine**: 6 built-in templates + custom JavaScript/C# scripting with syntax highlighting
- 👥 **Multi-tenant Architecture**: Secure tenant isolation, tenant switching, automatic personal workspace creation
- 🔐 **Secure Authentication**: OAuth 2.0 Authorization Code Flow with PKCE for secure SPA authentication
- 👤 **Role-based Access Control**: SuperAdmin, Admin, and User roles with permission-based authorization
- 📈 **Execution Monitoring**: Detailed logs, timeline view, progress tracking, and execution history
- 🔍 **Schema Management**: Auto-detection, manual definition, versioning, and change tracking
- 🔑 **API Key Management**: Generate and manage API keys for programmatic access
- 🤖 **AI Assistant**: Context-aware chatbot powered by Google Gemini AI for user guidance
- 🌍 **Internationalization**: Full i18n support with 6 languages (en, es, fr, de, it, pt)
- 🎯 **Metadata Service**: Centralized configuration management with automatic translation of dropdown options
- 🎨 **Theme Support**: Light and dark modes with Material Design components
- 📱 **Mobile-Friendly**: Responsive design that works seamlessly across desktop and mobile devices

## Tech Stack

- **Frontend Framework**: Vue 3 with Composition API (script setup syntax)
- **Build Tool**: Vite (fast dev server and optimized builds)
- **UI Framework**: Vuetify 3 (Material Design components)
- **State Management**: Pinia stores (auth, tenant)
- **Router**: Vue Router with route guards (authentication, admin roles)
- **HTTP Client**: Axios with interceptors for token management
- **Internationalization**: Vue I18n (6 languages: en, es, fr, de, it, pt)
- **Styling**: SASS with CSS custom properties for theming
- **Code Highlighting**: Prism.js (JavaScript/C# syntax highlighting in transformations)
- **File Processing**: 
  - PapaParse for CSV parsing
  - XLSX for Excel file processing
- **AI Integration**: Google Gemini AI for context-aware chatbot assistant
- **Authentication**: OAuth 2.0 Authorization Code Flow with PKCE (RFC 7636)

## Testing

This project uses **Vitest** for unit testing with comprehensive test coverage across all components, composables, services, and stores.

### Running Tests

```bash
# Run all tests once
npm run test:run

# Run tests in watch mode
npm run test

# Run tests with coverage report
npm run test:coverage

# Run specific test suites
npm run test:services      # Test services layer
npm run test:composables   # Test composables
npm run test:components    # Test components
npm run test:stores        # Test Pinia stores
```

### Test Structure

- **Services**: API service functions and business logic
- **Composables**: Vue composables for shared logic
- **Components**: Vue components with interaction testing
- **Stores**: Pinia store state management and actions
- **Utils**: Utility functions and helpers

### CI/CD

Tests are automatically run on every push via GitHub Actions. The CI pipeline:
1. Runs the complete test suite
2. Generates coverage reports
3. Builds the application
4. Deploys to GitHub Pages (if on main branch)

## Prerequisites

- Node.js (v16 or higher recommended)
- npm (comes with Node.js)

## Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd MultiTenantETL.Vue
```

2. Install dependencies:
```bash
npm install
```

3. Start the development server:
```bash
npm run dev
```

The application will be available at `http://localhost:5173`

## Configuration

### API Backend

The frontend expects a REST API backend (ASP.NET Core Web API). Configure the API URL:

**Development** (default in `src/config/api.js`):
```javascript
// Default: http://localhost:5000/api
// Backend runs on: https://localhost:7288 or http://localhost:5244
```

**Environment Variables**:

Create `.env` file for development:
```env
VITE_API_URL=http://localhost:5000/api
VITE_GEMINI_API_KEY=your-gemini-api-key
```

Create `.env.production` for production:
```env
VITE_API_URL=https://your-api-url.com/api
VITE_GEMINI_API_KEY=your-gemini-api-key
```

**Note**: The backend API runs on ports 7288 (HTTPS) or 5244 (HTTP) by default. Ensure CORS is configured in the backend for `http://localhost:5173`.

### Authentication

The application uses **OAuth 2.0 Authorization Code Flow with PKCE** (RFC 7636) for secure authentication:

- **Public Client**: No client secret required (SPA-safe)
- **PKCE**: Proof Key for Code Exchange prevents authorization code interception attacks
- **OpenIddict**: Backend OAuth 2.0 server (ASP.NET Core)
- **Token Management**: 
  - Access tokens (15 min) - stored in localStorage
  - Refresh tokens (7 days) - stored in localStorage
  - ID tokens - JWT with user claims
- **Security Features**:
  - State parameter for CSRF protection
  - Single-use authorization codes
  - Code verifier proves authorization request origin
  - Automatic token refresh with Axios interceptors

**Authentication Flow**:
1. User clicks login → Redirects to `/login`
2. User submits credentials
3. Frontend generates PKCE code verifier and challenge
4. Browser redirects to backend authorization endpoint with challenge
5. Backend validates credentials and issues single-use authorization code
6. Callback page (`/auth/callback`) exchanges code for tokens using verifier
7. Tokens stored in localStorage, user redirected to dashboard

**Implementation Details**:
- PKCE utilities: `src/utils/pkce.js` (code verifier/challenge generation)
- Auth service: `src/services/authService.js` (OAuth flow implementation)
- Auth store: `src/stores/auth.js` (token management, user state)
- Callback handler: `src/views/AuthCallbackView.vue` (token exchange)

See [OAuth PKCE Documentation](docs/guides/oauth-pkce.md) for detailed documentation.

### Google Gemini AI (Optional)

For the AI chatbot assistant, configure your Gemini API key:

**Development** (`.env`):
```env
VITE_GEMINI_API_KEY=your-gemini-api-key
```

**Production** (`.env.production`):
```env
VITE_GEMINI_API_KEY=your-gemini-api-key
```

Get your API key from [Google AI Studio](https://makersuite.google.com/app/apikey).

**Features**:
- Context-aware responses based on current page
- Multi-language support (responds in user's selected language)
- ETL pipeline guidance and troubleshooting
- Connector configuration help
- Transformation syntax assistance

## Available Scripts

```bash
# Development
npm run dev          # Start dev server on http://localhost:5173

# Production
npm run build        # Build for production (outputs to dist/)
npm run preview      # Preview production build locally

# Code Quality
npm run lint         # Run ESLint and auto-fix issues

# Testing
npm run test:all     # Run all tests (services, composables, components)
npm run test:services    # Run service layer tests (115 tests)
npm run test:composables # Run composable tests (331 tests)
npm run test:components  # Run component tests (18 tests)
npm run test:coverage    # Run tests with coverage report
npm run test:ui          # Run tests in UI mode
```

## Build Configuration

- **Base path**: `/MultiTenantETL.Vue/` (configured for GitHub Pages deployment)
- **Dev server port**: 5173
- **Path alias**: `@` → `src/`
- **Build tool**: Vite with optimized production builds

## Project Structure

```
src/
├── App.vue                 # Root component with layouts and global notifications
├── main.js                 # Application entry point, Vuetify and Pinia setup
├── components/             # Reusable UI components
│   ├── dashboard/          # Dashboard-specific components
│   ├── connector/          # Connector components (wizard, form, schema editor)
│   ├── dialogs/            # Modal dialogs (confirmation, etc.)
│   ├── executions/         # Pipeline execution components
│   ├── form/               # Form input components
│   ├── layouts/            # Layout components (Authenticated, Guest)
│   ├── notifications/      # Notification components
│   ├── pipeline/           # Pipeline-specific components (wizard, field mapping)
│   ├── settings/           # Settings page components
│   ├── table/              # Table components (filters, etc.)
│   ├── tenants/            # Tenant management components
│   ├── transformation/     # Transformation components
│   ├── users/              # User management components
│   ├── AiChatbot.vue       # AI assistant chatbot
│   ├── LanguageSwitcher.vue # Language selection component
│   └── TenantSelector.vue  # Tenant switching component
├── composables/            # Reusable composition functions
│   ├── useDashboard.js
│   ├── useConnector.js
│   ├── useFormValidation.js
│   ├── useGlobalState.js
│   ├── useLocale.js
│   ├── useMetadata.js          # Raw metadata access
│   ├── usePipeline.js
│   ├── usePipelineForm.js
│   ├── useTransformation.js
│   └── useTranslatedMetadata.js # Auto-translating metadata
├── config/                 # Configuration files
│   └── api.js              # API base URL configuration
├── locales/                # Internationalization (i18n)
│   ├── en.json             # English translations
│   ├── es.json             # Spanish translations
│   ├── fr.json             # French translations
│   ├── index.js            # i18n configuration
│   └── README.md           # Translation guide
├── router/                 # Vue Router configuration
│   └── index.js            # Route definitions and navigation guards
├── services/               # API service layer
│   ├── api.js              # Axios instance and interceptors
│   ├── authService.js      # OAuth 2.0 authentication with PKCE
│   ├── dashboardService.js # Dashboard data API calls
│   ├── connectorService.js # Connector CRUD operations
│   ├── geminiService.js    # Google Gemini AI integration
│   ├── metadataService.js  # Centralized metadata/config service
│   ├── pipelineService.js  # Pipeline CRUD operations
│   ├── schemaService.js    # Schema management
│   ├── tenantService.js    # Tenant management API calls
│   ├── transformationService.js # Transformation operations
│   └── userService.js      # User management API calls
├── utils/                  # Utility functions
│   ├── pkce.js             # PKCE utilities for OAuth 2.0
│   └── jwtHelper.js        # JWT token parsing and validation
├── stores/                 # Pinia state stores
│   ├── auth.js             # Authentication state
│   └── tenant.js           # Tenant context state
├── styles/                 # Global styles
│   └── global.scss         # Global SCSS styles
└── views/                  # Page-level components (route targets)
    ├── AuthCallbackView.vue    # OAuth callback handler
    ├── DashboardView.vue
    ├── ConnectorFormView.vue
    ├── ConnectorsView.vue
    ├── ExecutionsView.vue
    ├── LoginView.vue
    ├── PipelineDetailsView.vue
    ├── PipelineFormView.vue
    ├── PipelinesView.vue
    ├── SettingsView.vue
    ├── TenantsView.vue
    ├── TransformationsView.vue
    └── UsersView.vue
```

## Architecture

### Component Organization
- **Views**: Page-level components mapped to routes
- **Components**: Organized by feature domain (dashboard, pipeline, connector, etc.)
- **Layouts**: AuthenticatedLayout (with sidebar) and GuestLayout (login page)

### State Management
- **Pinia stores** for global state (authentication, tenant context)
- **Composables** for reusable business logic and API interactions
- **Provide/inject** for global notification system
- **Metadata Service** for centralized configuration and dropdown options

### Service Layer
- All API calls go through service modules in `src/services/`
- Services use a centralized Axios instance with interceptors
- Consistent error handling and response formatting

### Routing
- Route guards enforce authentication and admin role requirements
- Lazy-loaded route components for better performance
- Meta fields: `requiresAuth`, `requiresAdmin`, `guest`

## Key Features in Detail

### 🔄 Pipeline Management
- **Visual Field Mapping**: Drag-and-drop field mapping editor with validation
- **Pipeline Wizard**: Step-by-step pipeline creation with guided setup
- **Scheduling**: Automated pipeline runs with configurable schedules
- **Execution Monitoring**: Real-time progress tracking with detailed logs and timeline view
- **Pipeline Steps**: Configure Extract, Transform, Load operations
- **Schema Management**: Auto-detection and manual schema definition with versioning
- **Pipeline Cloning**: Duplicate existing pipelines for quick setup

### 🔌 Connectors
**Supported Types**:
- **Databases**: SQL Server, PostgreSQL, MySQL (with connection testing and schema auto-detection)
- **Files**: CSV (PapaParse), Excel/XLSX (XLSX library), JSON (with file preview and validation)
- **APIs**: REST APIs with configurable endpoints, authentication, and headers

**Features**:
- Connection testing and validation
- Secure credential management
- Schema versioning and change tracking
- Bidirectional support (sources and destinations)
- File preview and data validation

### 🔍 Transformations
**Built-in Templates** (6 types):
1. **Filter**: Filter rows based on field values with operators (equals, contains, greater than, less than, starts with, ends with, etc.)
2. **Map**: Transform field values using key-value mappings with default values
3. **Trim**: Remove leading/trailing whitespace from text fields
4. **Case Convert**: Convert text to uppercase, lowercase, title case, or camelCase
5. **Substring**: Extract portions of text from fields with start/end positions
6. **Replace**: Find and replace text or patterns (with regex support)

**Custom Scripts**:
- JavaScript or C# transformation logic
- Prism.js syntax highlighting
- Code editor with line numbers
- Validation and error handling

### 👥 Multi-tenant Support
- **Secure Isolation**: Complete data isolation between tenants
- **Tenant Switching**: Seamless context switching with TenantSelector component
- **Automatic Workspace**: Personal workspace created on registration
- **Tenant Management**: Full CRUD operations for SuperAdmin/Admin roles
- **Identifier Validation**: Unique tenant identifier checking

### 👤 User Management
- **Roles**: SuperAdmin, Admin, User with permission-based authorization
- **User CRUD**: Create, read, update, delete operations
- **Profile Management**: Name, email, password customization
- **API Keys**: Generate and manage API keys for programmatic access
- **User Search**: Filtering and search capabilities
- **Activation/Deactivation**: Account status management

### 🎨 Theme Support
- **Light/Dark Modes**: Smooth theme transitions
- **Customizable Colors**: Theme colors defined in `src/main.js`
- **CSS Custom Properties**: Consistent styling across components
- **Responsive Design**: Vuetify breakpoints for mobile/tablet/desktop
- **Touch-Optimized**: Mobile-friendly interface

### 🌍 Internationalization (i18n)
**Supported Languages**: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt)

**Features**:
- Full UI translation coverage
- Language persistence in localStorage
- Automatic Vuetify component translation
- AI chatbot responds in user's selected language
- Metadata service provides translated dropdown options
- Language switcher in navigation bar
- Easy to add new languages (create JSON file in `src/locales/`)

**Translation Files**: `src/locales/en.json`, `es.json`, `fr.json`, `de.json`, `it.json`, `pt.json`

## Backend API

The frontend requires the ASP.NET Core Web API backend to be running. See the backend README at `../MultiTenantETL/README.md` for setup instructions.

**Backend Repository**: Located in `../MultiTenantETL/`

**Quick Start**:
```bash
# Terminal 1: Start backend API
cd ../MultiTenantETL/src/MultiTenantETL.API
dotnet run

# Terminal 2: Start frontend
cd MultiTenantETL.Vue
npm run dev
```

**Backend Features**:
- OAuth 2.0 Authorization Code Flow with PKCE
- Multi-tenant architecture with complete isolation
- Permission-based authorization
- PostgreSQL database with EF Core
- Email integration with Azure Communication Services

## Documentation

Detailed documentation is organized in the `docs/` folder:

### Guides
- [OAuth PKCE Implementation](docs/guides/oauth-pkce.md) - OAuth 2.0 Authorization Code Flow with PKCE
- [Configuration Constants](docs/guides/configuration-constants.md) - Backend-driven configuration system
- [Quick Start](docs/guides/quick-start.md) - Quick start for using app constants
- [Troubleshooting](docs/guides/troubleshooting.md) - Common issues and solutions

### API Reference
- [API Index](docs/api/README.md) - Complete API specification index
- [Connectors API](docs/api/connectors-api.md) - Data source and destination management
- [Pipelines API](docs/api/pipelines-api.md) - ETL pipeline configuration
- [Transformations API](docs/api/transformations-api.md) - Data transformation logic
- [Executions API](docs/api/executions-api.md) - Pipeline execution tracking
- [Dashboard API](docs/api/dashboard-api.md) - Dashboard statistics
- [Metadata API](docs/api/metadata-api.md) - Centralized configuration

### Backend Development
- [Backend Overview](docs/backend/README.md) - Backend documentation status and setup
- [Authentication Guide](docs/backend/auth/authentication-guide.md) - Complete auth/authz implementation
- [Authorization Matrix](docs/backend/authorization-matrix.md) - Permission-based access control
- [Background Jobs](docs/backend/background-jobs.md) - Hangfire configuration
- [Real-time SignalR](docs/backend/realtime-signalr.md) - WebSocket implementation

### Features
- [Dynamic Token Generation](docs/features/dynamic-token-generation.md) - API connector token management

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
