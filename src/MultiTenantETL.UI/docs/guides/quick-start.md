# Quick Start: Using App Constants

## For Developers

### Using Role Constants

**Before** (hardcoded):
```javascript
if (user.role === 'SuperAdmin') { ... }
```

**After** (from backend):
```javascript
import { getRoles } from '@/config/constants'

const roles = getRoles()
if (user.role === roles.superAdmin) { ... }
```

Or use the helper functions:
```javascript
import { isSuperAdmin, isAdmin } from '@/utils/jwtHelper'

if (isSuperAdmin()) { ... }
if (isAdmin()) { ... }
```

### Using OAuth Configuration

**Before** (hardcoded):
```javascript
const CLIENT_ID = 'multitenant-etl-spa'
```

**After** (from backend):
```javascript
import { getOAuthConfig } from '@/config/constants'

const oauthConfig = getOAuthConfig()
console.log(oauthConfig.clientId) // 'multitenant-etl-spa'
console.log(oauthConfig.scopes)   // ['openid', 'email', ...]
```

### Checking Permissions

**Important**: Permission format changed from colon to dot notation!

**Before**:
```javascript
if (hasPermission('pipelines:create')) { ... }
```

**After**:
```javascript
if (hasPermission('pipelines.create')) { ... }
```

Common permissions:
- `pipelines.create`, `pipelines.read`, `pipelines.update`, `pipelines.delete`
- `connectors.create`, `connectors.read`, `connectors.update`, `connectors.delete`
- `users.create`, `users.read`, `users.update`, `users.delete`
- `tenants.create`, `tenants.read`, `tenants.update`, `tenants.delete`
- Wildcards: `pipelines.*`, `*.read`, `*.*`

### Using Supported Languages

```javascript
import { getSupportedLanguages } from '@/config/constants'

const languages = getSupportedLanguages()
// [
//   { code: 'en', name: 'English', nativeName: 'English' },
//   { code: 'es', name: 'Spanish', nativeName: 'Español' },
//   ...
// ]
```

## Backend Configuration

### Changing OAuth Client ID

Edit `appsettings.json`:
```json
{
  "OpenIddict": {
    "Clients": {
      "Spa": {
        "ClientId": "your-custom-client-id"
      }
    }
  }
}
```

The frontend will automatically use the new client ID on next load.

### Changing OAuth Scopes

Edit `MultiTenantETL.Domain/Constants/MetadataConstants.cs`:
```csharp
public static class OAuth
{
    public static readonly string[] DefaultScopes = new[]
    {
        "openid",
        "email",
        "profile",
        "roles",
        "api",
        "offline_access",
        "custom_scope"  // Add your custom scope
    };
}
```

### Adding New Roles

1. Add to `MultiTenantETL.Domain/Constants/Roles.cs`:
```csharp
public const string CustomRole = "CustomRole";
```

2. Update `MetadataService.GetAppConstants()`:
```csharp
Roles = new RolesDto
{
    SuperAdmin = Roles.SuperAdmin,
    TenantAdmin = Roles.TenantAdmin,
    User = Roles.User,
    Viewer = Roles.Viewer,
    CustomRole = Roles.CustomRole  // Add this
}
```

3. Update `AppConstantsDto.RolesDto`:
```csharp
public string CustomRole { get; set; } = string.Empty;
```

Frontend will automatically receive the new role.

### Adding New Languages

1. Add to `MultiTenantETL.Domain/Constants/MetadataConstants.cs`:
```csharp
public static class SupportedLanguages
{
    public static readonly (string Code, string Name, string NativeName)[] Languages = new[]
    {
        // ... existing languages
        ("ja", "Japanese", "日本語")
    };
}
```

The `MetadataService` will automatically pick it up.

## Troubleshooting

### Constants Not Loading

**Symptom**: Console shows "Using fallback constants - backend unavailable"

**Solutions**:
1. Ensure backend is running on `http://localhost:5000`
2. Check CORS configuration in backend
3. Verify `/api/metadata/app-constants` endpoint is accessible
4. Check browser network tab for errors

### Permission Checks Failing

**Symptom**: User has permission but `hasPermission()` returns false

**Solution**: Ensure you're using dot notation (`.`) not colon (`:`)
```javascript
// Wrong
hasPermission('pipelines:create')

// Correct
hasPermission('pipelines.create')
```

### Role Checks Not Working

**Symptom**: `isSuperAdmin()` or `isAdmin()` returns false incorrectly

**Solution**: 
1. Check JWT token has correct role claim
2. Verify constants initialized: `AppConstants._initialized` should be `true`
3. Hard refresh browser (Ctrl+Shift+R) to clear cache

## Development Workflow

1. **Start Backend**:
   ```bash
   cd MultiTenantETL/src/MultiTenantETL.API
   dotnet run
   ```

2. **Start Frontend**:
   ```bash
   cd MultiTenantETL.Vue
   npm run dev
   ```

3. **Verify Initialization**:
   - Open browser console
   - Look for: "App constants initialized from backend"
   - Check `AppConstants` object in console

4. **Test Changes**:
   - Modify backend constants
   - Restart backend
   - Hard refresh frontend (Ctrl+Shift+R)
   - Verify new values loaded

## Best Practices

1. **Always use getter functions** instead of accessing `AppConstants` directly
2. **Check initialization** in critical paths if needed
3. **Provide fallbacks** for offline/development scenarios
4. **Use helper functions** (`isSuperAdmin()`, `hasPermission()`) when available
5. **Document new constants** when adding them

## Examples

### Complete Permission Check Example
```javascript
import { hasPermission } from '@/utils/jwtHelper'

export default {
  setup() {
    const canCreatePipeline = computed(() => 
      hasPermission('pipelines.create')
    )
    
    const canManageUsers = computed(() => 
      hasPermission('users.*')
    )
    
    return { canCreatePipeline, canManageUsers }
  }
}
```

### Complete Role Check Example
```javascript
import { isSuperAdmin, isAdmin } from '@/utils/jwtHelper'
import { getRoles } from '@/config/constants'

export default {
  setup() {
    const roles = getRoles()
    const user = getCurrentUser()
    
    const isSuperAdminUser = computed(() => isSuperAdmin())
    const isAdminUser = computed(() => isAdmin())
    const isSpecificRole = computed(() => user?.role === roles.viewer)
    
    return { isSuperAdminUser, isAdminUser, isSpecificRole }
  }
}
```
