# Dynamic Token Generation Feature

## Overview
Added support for dynamic token generation in API connectors. This allows APIs that require fresh tokens to be called automatically during pipeline execution without manual token updates.

## Implementation

### Backend Changes
1. **ApiConfig Model** (`MultiTenantETL.Application/Connectors/Models/ConnectorDtos.cs`)
   - Added `UseDynamicToken` flag
   - Added `TokenEndpointUrl` - URL to call for token generation
   - Added `TokenEndpointMethod` - HTTP method (POST/GET)
   - Added `TokenEndpointHeaders` - Headers for token request
   - Added `TokenEndpointBody` - JSON body for token request
   - Added `TokenResponsePath` - JSON path to extract token from response
   - Added `TokenExpirySeconds` - Optional token caching duration

### Frontend Changes
1. **ConnectorWizard** (`src/components/connector/ConnectorWizard.vue`)
   - Added toggle switch for "Use Dynamic Token" under Bearer authentication
   - When enabled, shows fields for:
     - Token endpoint URL
     - HTTP method (POST/GET)
     - Request body (JSON)
     - Request headers (JSON)
     - Token response path (e.g., "access_token" or "data.token")
     - Cache duration (optional)

2. **Translations** (`src/locales/en.json`)
   - Added translation keys for all new fields
   - Added helpful hints for each field

## Usage

### Static Bearer Token (existing behavior)
1. Select "Bearer" as authentication type
2. Leave "Use Dynamic Token" toggle OFF
3. Enter your long-lived bearer token

### Dynamic Token Generation (new feature)
1. Select "Bearer" as authentication type
2. Enable "Use Dynamic Token" toggle
3. Configure token endpoint:
   - **Token Endpoint URL**: `https://api.example.com/auth/token`
   - **HTTP Method**: POST (or GET)
   - **Request Body**: `{"client_id": "xxx", "client_secret": "yyy", "grant_type": "client_credentials"}`
   - **Request Headers**: `{"Content-Type": "application/json"}`
   - **Token Response Path**: `access_token` (or `data.token` if nested)
   - **Cache Duration**: `3600` (optional - cache token for 1 hour)

## Next Steps (Backend Implementation Required)

The frontend is ready, but the backend needs to implement:

1. **Token Generation Service**
   - HTTP client to call token endpoint
   - Parse response using TokenResponsePath
   - Cache tokens based on TokenExpirySeconds

2. **Pipeline Execution Integration**
   - Before making API calls, check if UseDynamicToken is true
   - Generate/retrieve cached token
   - Inject token as "Bearer {token}" into Authorization header

3. **Token Caching**
   - Store tokens in memory with expiry
   - Refresh when expired
   - Handle token generation failures gracefully

## Example Use Cases

- **Client Credentials OAuth**: APIs that require client_id/client_secret to get access token
- **API Key Exchange**: APIs that exchange API keys for short-lived tokens
- **Custom Auth Flows**: Any API with a token generation endpoint
