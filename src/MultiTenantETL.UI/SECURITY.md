# Security Configuration

## Content Security Policy (CSP)

The application implements Content Security Policy headers to prevent XSS and code injection attacks.

### Current CSP Configuration (Development)

Located in `index.html`:

```
default-src 'self'
script-src 'self' 'unsafe-inline' 'unsafe-eval'
style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net
font-src 'self' data: https://cdn.jsdelivr.net
img-src 'self' data: https: blob:
connect-src 'self' http://localhost:* https://localhost:* https://generativelanguage.googleapis.com ws://localhost:*
base-uri 'self'
form-action 'self'
```

**Note:** `localhost:*` wildcards are for development only. Replace with specific domains in production.

### Production CSP Configuration

**REQUIRED:** Before deploying to production, update the CSP in `index.html`:

```html
<meta http-equiv="Content-Security-Policy" content="
  default-src 'self';
  script-src 'self' 'unsafe-inline' 'unsafe-eval';
  style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net;
  font-src 'self' data: https://cdn.jsdelivr.net;
  img-src 'self' data: https: blob:;
  connect-src 'self' https://your-api-domain.com https://generativelanguage.googleapis.com;
  base-uri 'self';
  form-action 'self';
  upgrade-insecure-requests;
">
```

**Replace:**
- `https://your-api-domain.com` with your actual backend API URL
  - Example: `https://api.yourdomain.com`
  - Example: `https://your-app.azurewebsites.net`

**Add back:**
- `upgrade-insecure-requests` - Forces HTTPS in production

### Optional: Stricter CSP (Advanced)

For maximum security (requires build configuration):

1. **Remove `'unsafe-inline'` and `'unsafe-eval'`** from `script-src`
2. Use nonces or hashes for inline scripts
3. Requires Vite CSP plugin: `vite-plugin-csp`

```bash
npm install vite-plugin-csp --save-dev
```

Update `vite.config.js`:
```javascript
import cspPlugin from 'vite-plugin-csp'

export default defineConfig({
  plugins: [
    vue(),
    cspPlugin({
      policy: {
        'script-src': ["'self'", "'nonce-{NONCE}'"],
        'style-src': ["'self'", "'nonce-{NONCE}'", 'https://cdn.jsdelivr.net']
      }
    })
  ]
})
```

## Subresource Integrity (SRI)

### Material Design Icons

Currently using CDN without SRI hash due to dynamic version resolution (`@7.4.47`).

**To add SRI:**

1. Generate hash:
   ```bash
   curl -s https://cdn.jsdelivr.net/npm/@mdi/font@7.4.47/css/materialdesignicons.min.css | \
   openssl dgst -sha384 -binary | openssl base64 -A
   ```

2. Add to `<link>` tag:
   ```html
   <link 
     href="https://cdn.jsdelivr.net/npm/@mdi/font@7.4.47/css/materialdesignicons.min.css" 
     rel="stylesheet"
     integrity="sha384-[HASH_HERE]"
     crossorigin="anonymous"
   >
   ```

**Alternative: Self-host icons** (recommended for production):
```bash
npm install @mdi/font
```

Then import in `main.js`:
```javascript
import '@mdi/font/css/materialdesignicons.css'
```

Remove CDN link from `index.html`.

## Backend Security Headers

The backend API (`MultiTenantETL.API`) implements comprehensive security headers via `SecurityHeadersMiddleware`:

- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`
- `Content-Security-Policy` (API-specific)
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: geolocation=(), microphone=(), camera=()`

## DDoS Protection

### Backend (Kestrel)

Configured in `Program.cs`:
- Max request body: 30MB
- Max concurrent connections: 100
- Request timeout: 30s
- Min data rate: 240 bytes/sec (Slowloris protection)

### Rate Limiting

Configured via `AspNetCoreRateLimit`:
- Login: 5 attempts/minute
- Registration: 3 attempts/hour
- Password reset: 3 attempts/15min
- POST/PUT: 30/minute
- DELETE: 20/minute
- GET: 100/minute

### Frontend

- Request timeout: 30 seconds
- Automatic retry with exponential backoff (5xx errors)
- Client-side search filtering (no API spam)

## Token Storage

**Current:** localStorage (vulnerable to XSS)

**Recommended for production:** 
- Use httpOnly cookies for refresh tokens
- Keep access tokens in memory only
- Implement token rotation

## HTTPS

**Development:** HTTP allowed
**Production:** HTTPS enforced via:
- `upgrade-insecure-requests` CSP directive
- Backend HSTS header
- Redirect HTTP → HTTPS in web server config

## Environment Variables

Never commit sensitive values to git:
- API keys (Gemini, Azure Communication)
- OAuth client secrets
- Database connection strings
- Email service credentials

Use:
- `.env.local` for local development (gitignored)
- Azure Key Vault / AWS Secrets Manager for production
- User secrets for .NET backend (`dotnet user-secrets`)

## Production Deployment Checklist

### Critical (Must Do Before Launch)

- [ ] **Update CSP in `index.html`**
  - Replace `http://localhost:*` and `https://localhost:*` with your production API URL
  - Add `upgrade-insecure-requests` directive
  - Example: `connect-src 'self' https://api.yourdomain.com https://generativelanguage.googleapis.com`

- [ ] **Update `.env.production`**
  - Set `VITE_API_BASE_URL` to your production API URL
  - Set `VITE_GEMINI_API_KEY` if using AI chatbot

- [ ] **Backend: Update CORS in `appsettings.json`**
  - Replace `http://localhost:5173` with your production frontend URL
  - Example: `https://yourdomain.com`

- [ ] **Backend: Update OAuth redirect URIs**
  - In `DbSeeder.cs`, update OAuth client redirect URIs
  - Replace `http://localhost:5173/callback` with `https://yourdomain.com/callback`

- [ ] **Enable HTTPS enforcement**
  - Ensure `UseHttpsRedirection()` is enabled in production
  - Configure SSL certificate in hosting environment

- [ ] **Set secure environment variables**
  - Database connection strings
  - OAuth client secrets
  - Email service credentials (Azure Communication)
  - Gemini API key
  - Use Azure Key Vault, AWS Secrets Manager, or similar

### Important (Recommended)

- [ ] **Enable email confirmation**
  - Set `RequireEmailConfirmation: true` in backend `appsettings.json`
  - Configure Azure Communication Services or SMTP

- [ ] **Review rate limits**
  - Adjust based on expected traffic in `Program.cs`
  - Monitor 429 responses and adjust accordingly

- [ ] **Self-host Material Design Icons** (optional but recommended)
  ```bash
  npm install @mdi/font
  ```
  - Import in `main.js`: `import '@mdi/font/css/materialdesignicons.css'`
  - Remove CDN link from `index.html`

- [ ] **Configure monitoring**
  - Application Insights (Azure)
  - CloudWatch (AWS)
  - Error tracking (Sentry, Rollbar)
  - Uptime monitoring

- [ ] **Set up logging**
  - Centralized logging (ELK, Splunk, CloudWatch)
  - Audit log retention policy
  - Security event alerts

### Security Hardening (Optional)

- [ ] **Implement stricter CSP**
  - Remove `'unsafe-inline'` and `'unsafe-eval'`
  - Use nonces or hashes (requires Vite plugin)

- [ ] **Move tokens to httpOnly cookies**
  - Prevents XSS token theft
  - Requires backend changes to set cookies

- [ ] **Add HSTS preload**
  - Submit domain to HSTS preload list
  - Requires 1 year HSTS header

- [ ] **Implement security headers in web server**
  - Nginx/Apache/IIS configuration
  - Redundant with backend headers but adds defense in depth

- [ ] **Regular security audits**
  - Dependency updates (`npm audit`, `dotnet list package --vulnerable`)
  - Penetration testing
  - Code security scanning (SonarQube, Snyk)

## Quick Production Update Script

Create a file `deploy-production.sh`:

```bash
#!/bin/bash

# Frontend
cd MultiTenantETL.Vue
echo "Building frontend for production..."
npm run build

# Backend
cd ../MultiTenantETL/src/MultiTenantETL.API
echo "Building backend for production..."
dotnet publish -c Release -o ./publish

echo "✅ Build complete!"
echo "⚠️  Remember to:"
echo "  1. Update CSP in index.html"
echo "  2. Set environment variables"
echo "  3. Update CORS and OAuth redirect URIs"
echo "  4. Enable HTTPS"
```

## Post-Deployment Verification

After deploying, verify:

1. **HTTPS is working** - Visit your site, check for padlock icon
2. **CSP is active** - Check browser console for CSP violations
3. **API calls work** - Test login, data fetching
4. **OAuth flow works** - Complete login/logout cycle
5. **Rate limiting works** - Try rapid requests, expect 429 responses
6. **Security headers present** - Use https://securityheaders.com
7. **No console errors** - Check browser developer console
