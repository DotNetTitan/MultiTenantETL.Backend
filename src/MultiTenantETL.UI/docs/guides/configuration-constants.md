# Configuration Constants

## Overview

This document describes how application constants are managed between the backend and frontend to avoid hardcoding values that should be centrally configured.

## Problem Solved

Previously, the frontend had hardcoded values for:
- OAuth client ID and scopes
- Role names (SuperAdmin, TenantAdmin, User, Viewer)
- Supported languages
- Permission format (colon vs dot notation)

This created maintenance issues where changing backend configuration required frontend code changes.

## Solution

### Backend Implementation

**New Endpoint**: `GET /api/metadata/app-constants`

Returns:
```json
{
  "roles": {
    "superAdmin": "SuperAdmin",
    "tenantAdmin": "TenantAdmin",
    "user": "User",
    "viewer": "Viewer"
  },
  "oauthConfig": {
    "clientId": "multitenant-etl-spa",
    "scopes": ["openid", "email", "profile", "roles", "api", "offline_access"],
    "authorizeEndpoint": "/connect/authorize",
    "tokenEndpoint": "/connect/token",
    "revokeEndpoint": "/connect/revoke"
  },
  "supportedLanguages": [
    { "code": "en", "name": "English", "nativeName": "English" },
    { "code": "es", "name": "Spanish", "nativeName": "Español" },
    { "code": "fr", "name": "French", "nativeName": "Français" },
    { "code": "de", "name": "German", "nativeName": "Deutsch" },
    { "code": "it", "name": "Italian", "nativeName": "Italiano" },
    { "code": "pt", "name": "Portuguese", "nativeName": "Português" }
  ]
}
```

**Files Modified**:
- `MultiTenantETL.Application/Metadata/AppConstantsDto.cs` (new)
- `MultiTenantETL.Application/Interfaces/IMetadataService.cs`
- `MultiTenantETL.Infrastructure/Services/MetadataService.cs`
- `MultiTenantETL.API/Controllers/MetadataController.cs`
- `MultiTenantETL.Domain/Constants/MetadataConstants.cs` (added OAuth and SupportedLanguages)

### Frontend Implementation

**New Module**: `src/config/constants.js`

Provides:
- `initializeConstants()` - Fetches constants from backend on app startup
- `getRoles()` - Returns role name constants
- `getOAuthConfig()` - Returns OAuth configuration
- `getSupportedLanguages()` - Returns supported languages

**Files Modified**:
- `src/config/constants.js` (new)
- `src/main.js` - Initializes constants before mounting app
- `src/services/authService.js` - Uses OAuth config from constants
- `src/utils/jwtHelper.js` - Uses role constants and fixed permission format

## Key Changes

### 1. OAuth Configuration
**Before**:
```javascript
const CLIENT_ID = 'multitenant-etl-spa'
const SCOPES = { OPENID: 'openid', ... }
```

**After**:
```javascript
import { getOAuthConfig } from '@/config/constants'
const oauthConfig = getOAuthConfig()
// Use oauthConfig.clientId, oauthConfig.scopes
```

### 2. Role Checking
**Before**:
```javascript
export function isSuperAdmin() {
    return hasRole('SuperAdmin')
}
```

**After**:
```javascript
export function isSuperAdmin() {
    const { getRoles } = require('@/config/constants')
    const roles = getRoles()
    return hasRole(roles?.superAdmin || 'SuperAdmin')
}
```

### 3. Permission Format
**Changed from**: `resource:action` (e.g., `pipelines:create`)
**Changed to**: `resource.action` (e.g., `pipelines.create`)

This matches the backend `Permissions.cs` constants format.

## Fallback Behavior

If the backend is unavailable during initialization, the frontend falls back to default constants to ensure the app remains functional. This provides resilience during development or network issues.

## Benefits

1. **Single Source of Truth**: Backend controls all configuration
2. **Easier Maintenance**: Change OAuth client or roles in one place
3. **Consistency**: Frontend and backend always use same values
4. **Flexibility**: Easy to add tenant-specific configurations later
5. **Type Safety**: Backend DTOs ensure consistent structure

## Usage

### Adding New Constants

1. Add to `MetadataConstants.cs` in Domain layer (if it's metadata)
2. Add to `AppConstantsDto.cs` in Application layer
3. Update `MetadataService.GetAppConstants()` to map from constants
4. Update `src/config/constants.js` to expose the new constant
5. Use throughout frontend via getter functions

### Example: Adding Token Lifetimes

**Step 1 - Domain Constants**:
```csharp
// MultiTenantETL.Domain/Constants/MetadataConstants.cs
public static class TokenLifetimes
{
    public const int AccessTokenMinutes = 15;
    public const int RefreshTokenDays = 7;
}
```

**Step 2 - Application DTO**:
```csharp
// MultiTenantETL.Application/Metadata/AppConstantsDto.cs
public class AppConstantsDto
{
    // ... existing properties
    public TokenConfigDto TokenConfig { get; set; } = new();
}

public class TokenConfigDto
{
    public int AccessTokenLifetimeMinutes { get; set; }
    public int RefreshTokenLifetimeDays { get; set; }
}
```

**Step 3 - Service Implementation**:
```csharp
// MultiTenantETL.Infrastructure/Services/MetadataService.cs
public AppConstantsDto GetAppConstants()
{
    return new AppConstantsDto
    {
        // ... existing properties
        TokenConfig = new TokenConfigDto
        {
            AccessTokenLifetimeMinutes = MetadataConstants.TokenLifetimes.AccessTokenMinutes,
            RefreshTokenLifetimeDays = MetadataConstants.TokenLifetimes.RefreshTokenDays
        }
    };
}
```

**Step 4 - Frontend**:
```javascript
// src/config/constants.js
export function getTokenConfig() {
    return AppConstants.tokenConfig
}
```

## Testing

The constants are cached in memory after first fetch. To test changes:

1. Restart the backend with new configuration
2. Hard refresh the frontend (Ctrl+Shift+R)
3. Check browser console for "App constants initialized from backend"

## Migration Notes

- Permission format changed from colon (`:`) to dot (`.`) notation
- All role name references now use constants instead of hardcoded strings
- OAuth configuration is now dynamic and can be changed without frontend rebuild
