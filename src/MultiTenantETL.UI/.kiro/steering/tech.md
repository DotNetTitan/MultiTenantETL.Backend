# Technology Stack

## Frontend Framework

- **Vue 3** with Composition API (script setup syntax)
- **Vite** as build tool and dev server
- **Vuetify 3** for UI components and Material Design
- **Vue Router** for navigation with route guards
- **Pinia** for state management
- **Axios** for HTTP requests

## Additional Libraries

- **Vue I18n** for internationalization (multi-language support)
- **Prism.js** for syntax highlighting (JavaScript/C# code in transformations)
- **PapaParse** for CSV file parsing
- **XLSX** for Excel file processing
- **Google Gemini AI** for AI-powered chatbot assistant

## Development Tools

- **ESLint** for code linting with Vue plugin
- **SASS** for styling

## Common Commands

```bash
# Development
npm run dev          # Start dev server on http://localhost:5173

# Production
npm run build        # Build for production (outputs to dist/)
npm run preview      # Preview production build locally

# Code Quality
npm run lint         # Run ESLint and auto-fix issues
```

## Build Configuration

- Base path: `/MultiTenantETL.Vue/` (configured for GitHub Pages deployment)
- Dev server port: 5173
- Path alias: `@` → `src/`

## Backend API

The frontend expects a REST API backend at `http://localhost:5000/api` (see api-specification.md for full API contract). The backend should be implemented in ASP.NET Core Web API.

## Authentication

- **OAuth 2.0 Authorization Code Flow with PKCE** (RFC 7636)
- **OpenIddict** backend OAuth 2.0 server
- **Public Client**: No client secret (SPA-safe)
- **PKCE Utilities**: `src/utils/pkce.js` for code verifier/challenge generation
- **Token Management**: 
  - Access tokens (15 min) - stored in localStorage
  - Refresh tokens (7 days) - stored in localStorage
  - ID tokens - JWT with user claims
- **Security Features**:
  - State parameter for CSRF protection
  - Single-use authorization codes
  - Code verifier proves authorization request origin
- **Flow**: Login → Browser redirect → Authorization → Callback → Token exchange → Dashboard
- **Implementation**: See `docs/guides/oauth-pkce.md` for detailed documentation

## Theme System

- Supports light and dark themes
- Default theme: dark
- Theme colors defined in src/main.js
- CSS custom properties for consistent styling across components

## Internationalization (i18n)

- **Vue I18n** for multi-language support
- Currently supports: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt)
- Translation files located in `src/locales/`
- Language selection persisted in localStorage
- Automatic Vuetify component translation
- AI chatbot responds in user's selected language
- Easy to add new languages by creating new JSON translation files

## Metadata Service

- Centralized configuration management for dropdown options
- Supports both mock data (for development) and API backend
- Automatic translation of metadata labels based on user's language
- Cached in localStorage for performance
- Used for: data source types, auth types, file formats, transformation types, schedule frequencies, etc.
- Easy to switch between mock and real API by toggling `USE_MOCK_DATA` flag
