# Project Structure

## Directory Organization

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
│   ├── authService.js      # Authentication API calls
│   ├── dashboardService.js # Dashboard data API calls
│   ├── connectorService.js # Connector CRUD operations
│   ├── geminiService.js    # Google Gemini AI integration
│   ├── metadataService.js  # Centralized metadata/config service
│   ├── pipelineService.js  # Pipeline CRUD operations
│   ├── schemaService.js    # Schema management and versioning
│   ├── tenantService.js    # Tenant management API calls
│   ├── transformationService.js # Transformation operations
│   └── userService.js      # User management API calls
├── styles/                 # Global styles
│   └── global.scss         # Global SCSS styles
├── stores/                 # Pinia state stores
│   ├── auth.js             # Authentication state
│   └── tenant.js           # Tenant context state
└── views/                  # Page-level components (route targets)
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

## Architectural Patterns

### Component Organization
- **Views**: Page-level components mapped to routes
- **Components**: Organized by feature domain (dashboard, pipeline, connector, etc.)
- **Layouts**: AuthenticatedLayout (with sidebar) and GuestLayout (login page)

### State Management
- **Pinia stores** for global state (auth, tenant context)
- **Composables** for reusable business logic and API interactions
- **Provide/inject** for global notification system
- **Metadata Service** for centralized configuration and dropdown options with translation support

### Service Layer
- All API calls go through service modules in `src/services/`
- Services use the centralized Axios instance from `src/services/api.js`
- API base URL configured in `src/config/api.js`

### Routing
- Route guards enforce authentication and admin role requirements
- Lazy-loaded route components for better performance
- Meta fields: `requiresAuth`, `requiresAdmin`, `guest`

### Styling Approach
- Vuetify components for consistent Material Design
- Global CSS custom properties for theme consistency
- SASS for advanced styling when needed
- Theme-aware classes (`.v-theme--dark`, `.v-theme--light`)

### Internationalization (i18n)
- Vue I18n for multi-language support
- Translation files in `src/locales/` (en.json, es.json, fr.json)
- Language persistence in localStorage
- Automatic Vuetify component translation
- AI chatbot responds in user's selected language
- Metadata service provides translated dropdown options
