# Authentication & Security Specification

**For:** ASP.NET Core Web API  
**Version:** 2.0 (OpenIddict + ASP.NET Core Identity)  
**Last Updated:** 2025-11-19

---

## Overview

This document defines authentication and security implementation for the Multi-Tenant ETL platform using:

- **OpenIddict** - OAuth 2.0 & OpenID Connect server
- **ASP.NET Core Identity** - User management and authentication

### Key Features

✅ **Standards-Compliant** - OAuth 2.0 / OpenID Connect  
✅ **Multi-Tenant** - User can belong to multiple tenants  
✅ **Token Management** - Access tokens, refresh tokens, revocation  
✅ **Built-in Security** - Account lockout, password policies  
✅ **Production-Ready** - Battle-tested, enterprise-grade  

---

## Installation

```bash
# Core packages
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# OpenIddict packages
dotnet add package OpenIddict.AspNetCore
dotnet add package OpenIddict.EntityFrameworkCore
dotnet add package OpenIddict.Quartz


```

---

## Database Models

```csharp
// Application User (extends Identity User)
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Guid? CurrentTenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public virtual ICollection<UserTenant> UserTenants { get; set; }
    public virtual Tenant CurrentTenant { get; set; }
}

// Application Role (extends Identity Role)
public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; }
    public List<string> Permissions { get; set; }
}

// User-Tenant relationship (many-to-many)
public class UserTenant
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string RoleCode { get; set; }
    public bool IsActive { get; set; }
    
    public virtual ApplicationUser User { get; set; }
    public virtual Tenant Tenant { get; set; }
}
```

---

### DbContext Configuration

```csharp
public class ApplicationDbContext : IdentityDbContext<
    ApplicationUser, 
    ApplicationRole, 
    Guid,
    IdentityUserClaim<Guid>,
    IdentityUserRole<Guid>,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<UserTenant> UserTenants { get; set; }
    // ... other DbSets

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Customize Identity table names
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // User-Tenant relationship
        builder.Entity<UserTenant>()
            .HasKey(ut => new { ut.UserId, ut.TenantId });

        builder.Entity<UserTenant>()
            .HasOne(ut => ut.User)
            .WithMany(u => u.UserTenants)
            .HasForeignKey(ut => ut.UserId);

        builder.Entity<UserTenant>()
            .HasOne(ut => ut.Tenant)
            .WithMany()
            .HasForeignKey(ut => ut.TenantId);
    }
}
```

---

### OpenIddict Configuration

```csharp
// Startup.cs or Program.cs
public void ConfigureServices(IServiceCollection services)
{
    // Add Identity
    services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false; // Set to true in production
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // Add OpenIddict
    services.AddOpenIddict()

        // Register the OpenIddict core components
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<ApplicationDbContext>();
            
            // Enable Quartz integration for token cleanup
            options.UseQuartz();
        })

        // Register the OpenIddict server components
        .AddServer(options =>
        {
            // Enable the token endpoints
            options.SetTokenEndpointUris("/connect/token")
                   .SetAuthorizationEndpointUris("/connect/authorize")
                   .SetLogoutEndpointUris("/connect/logout")
                   .SetUserinfoEndpointUris("/connect/userinfo")
                   .SetIntrospectionEndpointUris("/connect/introspect")
                   .SetRevocationEndpointUris("/connect/revoke");

            // Enable the flows
            options.AllowPasswordFlow()
                   .AllowRefreshTokenFlow()
                   .AllowClientCredentialsFlow()
                   .AllowAuthorizationCodeFlow()
                   .RequireProofKeyForCodeExchange();

            // Register scopes
            options.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Roles,
                "api"
            );

            // Register signing and encryption credentials
            if (env.IsDevelopment())
            {
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();
            }
            else
            {
                // Use real certificates in production
                options.AddEncryptionCertificate(
                    LoadCertificateFromStore("CN=MultiTenantETL-Encryption")
                );
                options.AddSigningCertificate(
                    LoadCertificateFromStore("CN=MultiTenantETL-Signing")
                );
            }

            // Register ASP.NET Core host and use JWT tokens
            options.UseAspNetCore()
                   .EnableTokenEndpointPassthrough()
                   .EnableAuthorizationEndpointPassthrough()
                   .EnableLogoutEndpointPassthrough()
                   .EnableUserinfoEndpointPassthrough();

            // Token lifetimes
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
            options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
        })

        // Register the OpenIddict validation components
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

    // Add authentication with external providers
    services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = Configuration["Authentication:Google:ClientId"];
            options.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
        })
        .AddMicrosoftAccount(options =>
        {
            options.ClientId = Configuration["Authentication:Microsoft:ClientId"];
            options.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];
        });

    // Add authorization
    services.AddAuthorization();
}

// Register OpenIddict in the middleware pipeline
public void Configure(IApplicationBuilder app)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
```

---

### Seed OpenIddict Applications

```csharp
public class OpenIddictSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        // Register the Vue.js SPA client
        if (await manager.FindByClientIdAsync("multitenant-etl-spa") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "multitenant-etl-spa",
                DisplayName = "Multi-Tenant ETL SPA",
                Type = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                
                // Refresh token flow
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                },
                
                RedirectUris =
                {
                    new Uri("http://localhost:5173/auth/callback"),
                    new Uri("https://app.example.com/auth/callback")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("http://localhost:5173/"),
                    new Uri("https://app.example.com/")
                }
            });
        }

        // Register API client for server-to-server
        if (await manager.FindByClientIdAsync("api-client") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "api-client",
                ClientSecret = "your-secret-here-minimum-32-characters-long",
                DisplayName = "API Client",
                Type = OpenIddictConstants.ClientTypes.Confidential,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "api"
                }
            });
        }
    }
}
```

---

## Authentication Controller (OpenIddict)

```csharp
[ApiController]
[Route("connect")]
public class AuthenticationController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;

    [HttpPost("token")]
    [Produces("application/json")]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordFlow(request);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshTokenFlow(request);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsFlow(request);
        }

        throw new NotImplementedException("The specified grant type is not implemented.");
    }

    private async Task<IActionResult> HandlePasswordFlow(OpenIddictRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Username) ??
                   await _userManager.FindByEmailAsync(request.Username);

        if (user == null)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The username/password couple is invalid."
                }));
        }

        // Validate password
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        
        if (!result.Succeeded)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        result.IsLockedOut ? "Account is locked out." :
                        "The username/password couple is invalid."
                }));
        }

        // Create claims principal
        var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());

        // Sign in
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenFlow(OpenIddictRequest request)
    {
        // Retrieve the claims principal stored in the refresh token
        var info = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Retrieve the user from the database
        var user = await _userManager.GetUserAsync(info.Principal);
        if (user == null)
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The refresh token is no longer valid."
                }));
        }

        // Ensure the user is still allowed to sign in
        if (!await _signInManager.CanSignInAsync(user))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user is no longer allowed to sign in."
                }));
        }

        // Create a new claims principal
        var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());

        // Sign in with the new principal
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
        ApplicationUser user, 
        ImmutableArray<string> scopes)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity;

        // Add custom claims
        identity.AddClaim(new Claim("tenant_id", user.CurrentTenantId?.ToString() ?? ""));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));

        // Set destinations for each claim
        identity.SetDestinations(static claim => claim.Type switch
        {
            // Never include the security stamp in tokens
            "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,

            // ID token + access token
            ClaimTypes.Name or ClaimTypes.Email or "tenant_id"
                => ImmutableArray.Create(
                    OpenIddictConstants.Destinations.AccessToken,
                    OpenIddictConstants.Destinations.IdentityToken),

            // Access token only
            _ => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken)
        });

        return principal;
    }
}
```

---

## Password Security

### Password Policy

```csharp
public class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;
    public const bool RequireUppercase = true;
    public const bool RequireLowercase = true;
    public const bool RequireDigit = true;
    public const bool RequireSpecialCharacter = true;
    public const string SpecialCharacters = "!@#$%^&*()_+-=[]{}|;:',.<>?";
}

public class PasswordValidator
{
    public ValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (password.Length < PasswordPolicy.MinimumLength)
            errors.Add($"Password must be at least {PasswordPolicy.MinimumLength} characters");

        if (password.Length > PasswordPolicy.MaximumLength)
            errors.Add($"Password cannot exceed {PasswordPolicy.MaximumLength} characters");

        if (PasswordPolicy.RequireUppercase && !password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter");

        if (PasswordPolicy.RequireLowercase && !password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter");

        if (PasswordPolicy.RequireDigit && !password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit");

        if (PasswordPolicy.RequireSpecialCharacter && 
            !password.Any(c => PasswordPolicy.SpecialCharacters.Contains(c)))
            errors.Add("Password must contain at least one special character");

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}
```

### Password Hashing

**Algorithm:** BCrypt with cost factor 12

```csharp
public class PasswordHasher
{
    private const int WorkFactor = 12; // 2^12 iterations

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
```

**Note:** Use `BCrypt.Net-Next` NuGet package

---

## Account Security

### Failed Login Protection

```csharp
public class AccountLockoutSettings
{
    public int MaxFailedAttempts = 5;
    public TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public TimeSpan FailedAttemptsWindow = TimeSpan.FromMinutes(5);
}

// Track failed attempts
public class FailedLoginAttempt
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string IpAddress { get; set; }
    public DateTime AttemptedAt { get; set; }
}

// Check and enforce lockout
public async Task<bool> IsAccountLocked(string email)
{
    var windowStart = DateTime.UtcNow.Subtract(_settings.FailedAttemptsWindow);
    
    var recentAttempts = await _db.FailedLoginAttempts
        .CountAsync(f => 
            f.Email == email && 
            f.AttemptedAt >= windowStart
        );

    return recentAttempts >= _settings.MaxFailedAttempts;
}
```

### Session Management

```csharp
// Refresh token in database
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string RevokedByIp { get; set; }
    public string ReplacedByToken { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}

// Revoke all user sessions
public async Task RevokeAllUserSessions(Guid userId)
{
    await _db.RefreshTokens
        .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
        .ExecuteUpdateAsync(s => s
            .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow)
        );
}
```

---

## CORS Configuration

### Settings

```json
// appsettings.json
{
  "CorsSettings": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "https://app.example.com"
    ],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"],
    "AllowedHeaders": ["Authorization", "Content-Type", "X-Tenant-Id"],
    "ExposedHeaders": ["Content-Disposition"],
    "MaxAgeSeconds": 3600
  }
}
```

### Implementation

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", builder =>
        {
            builder
                .WithOrigins(_corsSettings.AllowedOrigins)
                .WithMethods(_corsSettings.AllowedMethods)
                .WithHeaders(_corsSettings.AllowedHeaders)
                .WithExposedHeaders(_corsSettings.ExposedHeaders)
                .SetPreflightMaxAge(TimeSpan.FromSeconds(_corsSettings.MaxAgeSeconds));

            if (_corsSettings.AllowCredentials)
            {
                builder.AllowCredentials();
            }
        });
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseCors("AllowFrontend");
}
```

---

## Rate Limiting

### Configuration

```csharp
// Install: AspNetCoreRateLimit
public void ConfigureServices(IServiceCollection services)
{
    services.AddMemoryCache();
    
    services.Configure<IpRateLimitOptions>(options =>
    {
        options.EnableEndpointRateLimiting = true;
        options.StackBlockedRequests = false;
        options.HttpStatusCode = 429;
        options.RealIpHeader = "X-Real-IP";
        
        options.GeneralRules = new List<RateLimitRule>
        {
            // Auth endpoints - strict
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/login",
                Period = "1m",
                Limit = 5
            },
            new RateLimitRule
            {
                Endpoint = "POST:/api/auth/register",
                Period = "1h",
                Limit = 3
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
    
    services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
    services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
}

public void Configure(IApplicationBuilder app)
{
    app.UseIpRateLimiting();
}
```

### Custom Rate Limit Response

```csharp
options.QuotaExceededResponse = async (context, rule, retryAfter) =>
{
    context.Response.Headers["Retry-After"] = retryAfter;
    
    var response = new
    {
        error = new
        {
            code = "RATE_LIMIT_EXCEEDED",
            message = "Too many requests. Please try again later.",
            retryAfter = retryAfter
        }
    };
    
    context.Response.StatusCode = 429;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(response);
};
```

---

## API Key Management

### For External Integrations

```csharp
public class ApiKey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; }
    public string KeyHash { get; set; } // SHA256 hash
    public string KeyPrefix { get; set; } // First 8 chars for identification
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public List<string> Scopes { get; set; } // Permissions
}

// API Key Authentication Handler
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyValue))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = await ValidateApiKey(apiKeyValue);
        if (apiKey == null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new[]
        {
            new Claim("tenant_id", apiKey.TenantId.ToString()),
            new Claim("api_key_id", apiKey.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKey.Name)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
```

---

## Security Headers

```csharp
public void Configure(IApplicationBuilder app)
{
    app.Use(async (context, next) =>
    {
        // Prevent clickjacking
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        
        // Prevent MIME sniffing
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        // XSS protection
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        
        // HTTPS only
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains");
        
        // Content Security Policy
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';");
        
        // Referrer policy
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Permissions policy
        context.Response.Headers.Add("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=()");

        await next();
    });
}
```

---

## HTTPS Enforcement

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });

    services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    if (!env.IsDevelopment())
    {
        app.UseHsts();
    }
    
    app.UseHttpsRedirection();
}
```

---

## Input Validation & Sanitization

```csharp
// Always validate and sanitize user input
public class InputSanitizer
{
    public string SanitizeHtml(string input)
    {
        // Use HtmlSanitizer library
        var sanitizer = new HtmlSanitizer();
        return sanitizer.Sanitize(input);
    }

    public string SanitizeSql(string input)
    {
        // Always use parameterized queries
        // Never concatenate user input into SQL
        throw new InvalidOperationException("Use parameterized queries instead");
    }

    public string SanitizeFileName(string fileName)
    {
        // Remove path traversal attempts
        fileName = Path.GetFileName(fileName);
        
        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars));
    }
}
```

---

## Secrets Management

### Development
```bash
# Use User Secrets
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "your-secret-here"
```

### Production
```csharp
// Use Azure Key Vault or AWS Secrets Manager
public void ConfigureAppConfiguration(IConfigurationBuilder config)
{
    if (env.IsProduction())
    {
        var builtConfig = config.Build();
        var keyVaultUrl = builtConfig["KeyVaultUrl"];
        
        config.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential()
        );
    }
}
```

---

## Security Checklist

### ✅ Authentication
- [x] JWT with strong secret (256-bit minimum)
- [x] Short-lived access tokens (15 minutes)
- [x] Refresh token rotation
- [x] Token revocation on logout

### ✅ Passwords
- [x] BCrypt hashing (cost factor 12)
- [x] Strong password policy
- [x] Account lockout after failed attempts
- [x] No password in logs or errors

### ✅ Authorization
- [x] Role-based access control
- [x] Tenant isolation (RLS)
- [x] API key scopes

### ✅ Network Security
- [x] HTTPS only (HSTS)
- [x] CORS properly configured
- [x] Rate limiting
- [x] Security headers

### ✅ Data Security
- [x] Input validation
- [x] SQL injection prevention (parameterized queries)
- [x] XSS prevention (output encoding)
- [x] Secrets in Key Vault

### ✅ Monitoring
- [x] Failed login attempts logged
- [x] API key usage tracked
- [x] Unusual activity alerts
