# Troubleshooting: OAuth Config Undefined Error

## Error
```
TypeError: can't access property "scopes", oauthConfig is undefined
```

## Root Cause
The `oauthConfig` is undefined when trying to login, even though constants were initialized.

## Diagnostic Steps

### 1. Check if Backend is Running
```bash
# In MultiTenantETL/src/MultiTenantETL.API
dotnet run
```

Expected output: `Now listening on: https://localhost:7288`

### 2. Test the Endpoint Directly
Open browser and navigate to:
```
https://localhost:7288/api/metadata/app-constants
```

Expected response:
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
  "supportedLanguages": [...]
}
```

### 3. Check Browser Console
Look for these messages:
- ✅ "App constants initialized from backend" - Good
- ✅ Should show the actual constants object
- ❌ "OAuth config is null/undefined!" - Problem

### 4. Check Network Tab
- Open DevTools → Network tab
- Look for request to `/api/metadata/app-constants`
- Check:
  - Status: Should be 200
  - Response: Should contain `oauthConfig` object
  - CORS headers: Should be present

## Common Issues & Solutions

### Issue 1: Backend Not Running
**Symptom**: "Using fallback constants - backend unavailable"

**Solution**:
```bash
cd MultiTenantETL/src/MultiTenantETL.API
dotnet run
```

### Issue 2: CORS Error
**Symptom**: Console shows CORS error, request blocked

**Solution**: Check `appsettings.Development.json`:
```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

### Issue 3: Wrong API URL
**Symptom**: 404 error on constants request

**Solution**: Check `.env.development`:
```
VITE_API_BASE_URL=https://localhost:7288
```

### Issue 4: Certificate Error
**Symptom**: SSL/TLS error in console

**Solution**:
```bash
dotnet dev-certs https --trust
```

### Issue 5: Response Structure Mismatch
**Symptom**: "Invalid response structure from backend"

**Solution**: Check backend response matches expected structure. The backend should return camelCase JSON (ASP.NET Core default).

## Quick Fix: Use Fallback Constants

If you need to test immediately without backend:

1. The frontend now automatically falls back to hardcoded constants
2. Check console for: "Using fallback constants"
3. Login should work with default OAuth config

## Verify Fix

After applying solutions:

1. Hard refresh browser (Ctrl+Shift+R)
2. Check console for: "App constants initialized from backend"
3. Check console shows the constants object with `oauthConfig`
4. Try logging in

## Still Not Working?

Check the full initialization flow:

1. `main.js` calls `initializeConstants()`
2. Request to `/api/metadata/app-constants`
3. Response stored in `AppConstants` object
4. `getOAuthConfig()` returns the config
5. `authService.initiateLogin()` uses the config

Add debug logging:
```javascript
// In authService.js
console.log('Getting OAuth config...')
const oauthConfig = getOAuthConfig()
console.log('OAuth config:', oauthConfig)
```

## Contact

If issue persists, provide:
1. Browser console output (full)
2. Network tab screenshot
3. Backend console output
4. `.env.development` contents (without secrets)
