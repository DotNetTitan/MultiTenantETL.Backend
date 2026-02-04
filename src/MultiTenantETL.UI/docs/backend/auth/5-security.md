# 5. Security Features

**Purpose:** Rate limiting, CORS, security headers, input validation, and secrets management

---

## Rate Limiting

### Installation

```bash
dotnet add package AspNetCoreRateLimit
```

### Configuration

```csharp
// API/Program.cs
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// Required for rate limiting
builder.Services.AddMemoryCache();

// Configure IP rate limiting
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.ClientIdHeader = "X-ClientId";
    
    options.GeneralRules = new List<RateLimitRule>
    {
        // Authentication endpoints - very strict
        new RateLimitRule
        {
            Endpoint = "POST:/connect/token",
            Period = "1m",
            Limit = 5  // 5 login attempts per minute
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/register",
            Period = "1h",
            Limit = 3  // 3 registrations per hour
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/forgot-password",
            Period = "15m",
            Limit = 3  // 3 password reset requests per 15 min
        },
        
        // Write operations - moderate
        new RateLimitRule
        {
            Endpoint = "POST:*",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "PUT:*",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "DELETE:*",
            Period = "1m",
            Limit = 20
        },
        
        // Read operations - permissive
        new RateLimitRule
        {
            Endpoint = "GET:*",
            Period = "1m",
            Limit = 100
        },
        
        // Global fallback
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1s",
            Limit = 10
        }
    };
});

// Register rate limiting services
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

var app = builder.Build();

// Use rate limiting middleware
app.UseIpRateLimiting();

app.Run();
```

### Custom Rate Limit Response

```csharp
// In IpRateLimitOptions configuration
options.QuotaExceededResponse = async (context, rule, retryAfter) =>
{
    context.Response.Headers["Retry-After"] = retryAfter;
    
    var response = new ErrorResponse(
        AuthErrorCode.ValidationError,
        "Too many requests. Please try again later.");
    
    context.Response.StatusCode = 429;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(response);
};
```

---

## CORS Configuration

```csharp
// API/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",      // Vite dev server
                "http://localhost:3000",      // Alternative frontend port
                "https://app.yourdomain.com"  // Production frontend
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()  // Important for cookies/auth
            .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
});

var app = builder.Build();

// Use CORS - MUST be before UseAuthentication/UseAuthorization
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.Run();
```

### Dynamic CORS (from config)

```json
// appsettings.json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://app.yourdomain.com"
    ]
  }
}
```

```csharp
// Read from configuration
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

---

## Security Headers

```csharp
// API/Middleware/SecurityHeadersMiddleware.cs
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        
        // Prevent MIME sniffing
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        // XSS protection
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        
        // HTTPS only (HSTS)
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains; preload");
        
        // Content Security Policy
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';");
        
        // Referrer policy
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Permissions policy
        context.Response.Headers.Add("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");

        // Remove server header
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}

// Extension method
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}

// In Program.cs
app.UseSecurityHeaders();
```

---

## HTTPS Enforcement

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure HTTPS redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
    options.HttpsPort = 443;
});

// Add HSTS
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

// Use HTTPS redirection (not in development)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.Run();
```

---

## Input Validation & Sanitization

### Data Annotations

```csharp
public class RegisterRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(100, ErrorMessage = "Email must be less than 100 characters")]
    public string Email { get; set; }
    
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]",
        ErrorMessage = "Password must contain uppercase, lowercase, digit, and special character")]
    public string Password { get; set; }
    
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50, MinimumLength = 2)]
    [RegularExpression(@"^[a-zA-Z\s\-']+$", ErrorMessage = "Invalid characters in first name")]
    public string FirstName { get; set; }
}
```

### Sanitization Service

```csharp
// Application/Common/Interfaces/IInputSanitizer.cs
public interface IInputSanitizer
{
    string SanitizeString(string input);
    string SanitizeHtml(string input);
}

// Infrastructure/Security/InputSanitizer.cs
using System.Text.RegularExpressions;

public class InputSanitizer : IInputSanitizer
{
    public string SanitizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove potentially dangerous characters
        input = input.Trim();
        
        // Remove control characters
        input = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");
        
        // Remove SQL injection patterns
        input = Regex.Replace(input, @"('|(--)|;|\/\*|\*\/|xp_|sp_)", "", RegexOptions.IgnoreCase);
        
        // Remove script tags
        input = Regex.Replace(input, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase);
        
        return input;
    }

    public string SanitizeHtml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Basic HTML encoding
        input = System.Net.WebUtility.HtmlEncode(input);
        
        return input;
    }
}

// Register in Program.cs
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
```

---

## Secrets Management

### Development (User Secrets)

```bash
# Initialize user secrets
dotnet user-secrets init

# Add secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."
dotnet user-secrets set "AzureCommunicationServices:ConnectionString" "endpoint=..."
dotnet user-secrets set "AzureCommunicationServices:FromEmail" "noreply@domain.com"
```

### Production (Azure Key Vault)

```bash
# Install package
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Identity
```

```csharp
// API/Program.cs
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault
if (builder.Environment.IsProduction())
{
    var keyVaultName = builder.Configuration["KeyVaultName"];
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    
    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

// Now you can access secrets like normal configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
```

### Environment Variables

```csharp
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  }
}

// Set environment variable
// Windows: $env:DATABASE_CONNECTION_STRING="Host=..."
// Linux: export DATABASE_CONNECTION_STRING="Host=..."
```

---

## Password Hashing (BCrypt)

```csharp
// Install BCrypt.Net-Next
// Already configured in ASP.NET Core Identity

// Custom password hasher (if needed)
using BCrypt.Net;

// Infrastructure/Identity/BcryptPasswordHasher.cs
public class BcryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    public string HashPassword(ApplicationUser user, string password)
    {
        return BCrypt.HashPassword(password, workFactor: 12);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user, 
        string hashedPassword, 
        string providedPassword)
    {
        var isValid = BCrypt.Verify(providedPassword, hashedPassword);
        return isValid 
            ? PasswordVerificationResult.Success 
            : PasswordVerificationResult.Failed;
    }
}

// Register in Program.cs
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BcryptPasswordHasher>();
```

---

## Security Checklist

### Development
- [ ] User secrets configured
- [ ] HTTPS redirection disabled in development
- [ ] Email confirmation disabled in development
- [ ] Detailed error messages enabled

### Production
- [ ] Azure Key Vault or secrets manager configured
- [ ] HTTPS enforced with valid certificate
- [ ] HSTS enabled
- [ ] Security headers middleware added
- [ ] Rate limiting configured
- [ ] CORS restricted to known origins
- [ ] Password complexity enforced
- [ ] Account lockout enabled
- [ ] SQL injection protection (EF Core parameterization)
- [ ] XSS protection (input sanitization)
- [ ] CSRF protection (SameSite cookies)
- [ ] Error messages are generic (no sensitive info)
- [ ] Logging doesn't contain passwords/secrets
- [ ] Database connection uses least privilege account

---

**Next:** [6. Authorization](./6-authorization.md)
