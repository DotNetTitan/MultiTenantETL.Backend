# OAuth 2.0 Authorization Code Flow with PKCE

This application implements **Authorization Code Flow with PKCE (Proof Key for Code Exchange)** as defined in [RFC 7636](https://tools.ietf.org/html/rfc7636) for secure authentication in Single Page Applications (SPAs).

## Why PKCE?

PKCE enhances the security of the Authorization Code Flow by preventing authorization code interception attacks. It's the recommended flow for public clients (SPAs, mobile apps) that cannot securely store client secrets.

## Implementation Overview

### Backend (ASP.NET Core + OpenIddict)

**Configuration** (`Program.cs`):
```csharp
.AddServer(options =>
{
    options.SetAuthorizationEndpointUris("/connect/authorize")
           .SetTokenEndpointUris("/connect/token")
           .SetRevocationEndpointUris("/connect/revoke");

    options.AllowAuthorizationCodeFlow()
           .RequireProofKeyForCodeExchange();  // Enforces PKCE
    
    options.AllowRefreshTokenFlow();
})
```

**OAuth Client** (`DbSeeder.cs`):
```csharp
ClientId = "multitenant-etl-spa"
ClientType = OpenIddictConstants.ClientTypes.Public  // No client secret
Permissions = {
    OpenIddictConstants.Permissions.Endpoints.Authorization,
    OpenIddictConstants.Permissions.Endpoints.Token,
    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
    OpenIddictConstants.Permissions.ResponseTypes.Code
}
```

**Endpoints** (`AuthenticationController.cs`):
- `POST /connect/authorize` - Issues authorization code after validating credentials
- `POST /connect/token` - Exchanges authorization code for access/refresh tokens
- `POST /connect/revoke` - Revokes refresh tokens

### Frontend (Vue 3)

**PKCE Utilities** (`src/utils/pkce.js`):
- `generateCodeVerifier()` - Creates cryptographically random verifier
- `generateCodeChallenge(verifier)` - Generates SHA-256 hash of verifier
- `generateState()` - Creates CSRF protection token

**Authentication Flow** (`src/services/authService.js`):

1. **Generate PKCE Parameters**:
   ```javascript
   const codeVerifier = generateCodeVerifier()
   const codeChallenge = await generateCodeChallenge(codeVerifier)
   const state = generateState()
   ```

2. **Store PKCE Parameters** (for callback):
   ```javascript
   storePKCEParams(state, codeVerifier)
   ```

3. **Redirect to Authorization Endpoint**:
   ```javascript
   window.location.href = `${API_BASE}/connect/authorize?` + new URLSearchParams({
     client_id: 'multitenant-etl-spa',
     response_type: 'code',
     scope: 'openid email profile roles api offline_access',
     redirect_uri: 'http://localhost:5173/auth/callback',
     state: state,
     code_challenge: codeChallenge,
     code_challenge_method: 'S256',
     username: email,
     password: password
   })
   ```

4. **Backend Issues Authorization Code** and redirects to callback URL

5. **Callback Handler Extracts Code** from URL parameters

6. **Exchange Code for Tokens**:
   ```javascript
   POST /connect/token
   {
     client_id: 'multitenant-etl-spa',
     grant_type: 'authorization_code',
     code: authorizationCode,
     redirect_uri: 'http://localhost:5173/auth/callback',
     code_verifier: codeVerifier  // Proves we generated the challenge
   }
   ```

7. **Refresh Tokens**:
   ```javascript
   POST /connect/token
   {
     client_id: 'multitenant-etl-spa',
     grant_type: 'refresh_token',
     refresh_token: refreshToken,
     scope: 'openid email profile roles api offline_access'
   }
   ```

## Security Features

### PKCE Protection
- **Code Verifier**: Random 43-128 character string (base64url-encoded)
- **Code Challenge**: SHA-256 hash of code verifier
- **Verification**: Server validates code_verifier matches original code_challenge

### State Parameter
- Prevents CSRF attacks
- Generated per-request and validated on callback

### Token Storage
- Access Token: Short-lived (15 minutes), used for API requests
- Refresh Token: Long-lived (7 days), used to obtain new access tokens
- ID Token: Contains user claims (email, name, roles, tenant)

### No Client Secret
- Public client (SPA) doesn't store secrets
- PKCE provides security without client authentication

## Token Lifetimes

| Token Type | Lifetime | Purpose |
|------------|----------|---------|
| Access Token | 15 minutes | API authentication |
| Refresh Token | 7 days | Obtain new access tokens |
| ID Token | 15 minutes | User information |

## Scopes

- `openid` - OpenID Connect authentication
- `email` - User email address
- `profile` - User profile (name, etc.)
- `roles` - User roles and permissions
- `api` - Access to API resources
- `offline_access` - Refresh token grant

## Testing the Flow

### Using the Vue App

1. Start the backend:
   ```bash
   cd MultiTenantETL/src/MultiTenantETL.API
   dotnet run
   ```

2. Start the frontend:
   ```bash
   cd MultiTenantETL.Vue
   npm run dev
   ```

3. Login at `http://localhost:5173/login`
   - Email: `admin@multitenant-etl.com`
   - Password: `Admin@123456`

### Manual Testing with cURL

1. **Generate PKCE parameters** (use online tool or script):
   ```
   code_verifier: dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk
   code_challenge: E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM
   ```

2. **Request authorization code** (follow redirects to see the callback URL):
   ```bash
   curl -L -v http://localhost:5000/connect/authorize \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "client_id=multitenant-etl-spa" \
     -d "response_type=code" \
     -d "scope=openid email profile roles api offline_access" \
     -d "redirect_uri=http://localhost:5173/auth/callback" \
     -d "state=random_state_value" \
     -d "code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM" \
     -d "code_challenge_method=S256" \
     -d "username=admin@multitenant-etl.com" \
     -d "password=Admin@123456"
   ```

3. **Extract authorization code from redirect** (look for the final URL with `?code=...&state=...`)

4. **Exchange code for tokens**:
   ```bash
   curl -X POST http://localhost:5000/connect/token \
     -H "Content-Type: application/x-www-form-urlencoded" \
     -d "client_id=multitenant-etl-spa" \
     -d "grant_type=authorization_code" \
     -d "code=AUTHORIZATION_CODE_HERE" \
     -d "redirect_uri=http://localhost:5173/auth/callback" \
     -d "code_verifier=dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
   ```

### Browser Flow (Recommended)

The proper way to test is through the browser:

1. Open http://localhost:5173/login
2. Enter credentials and submit
3. Browser redirects to `/connect/authorize` with PKCE parameters
4. Backend validates credentials and redirects to `/auth/callback?code=...&state=...`
5. Callback page exchanges code for tokens
6. User is redirected to dashboard with valid session

## References

- [RFC 7636 - Proof Key for Code Exchange](https://tools.ietf.org/html/rfc7636)
- [OAuth 2.0 for Browser-Based Apps](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-browser-based-apps)
- [OpenIddict Documentation](https://documentation.openiddict.com/)
