# 1. Setup & Configuration

**Purpose:** Installation, database models, OpenIddict configuration, and error codes

---

## Installation

### Required NuGet Packages

```bash
# Core packages
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design

# OpenIddict
dotnet add package OpenIddict.AspNetCore
dotnet add package OpenIddict.EntityFrameworkCore
dotnet add package OpenIddict.Quartz

# Azure Communication Services
dotnet add package Azure.Communication.Email

# Security
dotnet add package BCrypt.Net-Next
dotnet add package AspNetCoreRateLimit
```

---

## Database Models

### ApplicationUser

```csharp
// Infrastructure/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

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
```

### ApplicationRole

```csharp
// Infrastructure/Identity/ApplicationRole.cs
using Microsoft.AspNetCore.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; }
}
```

### ITenantResource

```csharp
// Domain/Interfaces/ITenantResource.cs
public interface ITenantResource
{
    Guid TenantId { get; }
}
```

### Tenant

```csharp
// Domain/Entities/Tenant.cs
using MultiTenantETL.Domain.Interfaces;

public class Tenant : ITenantResource
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Slug { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Interface implementation
    public Guid TenantId => Id;
}
```

### UserTenant

```csharp
// Infrastructure/Identity/UserTenant.cs
public class UserTenant
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string RoleCode { get; set; }
    public bool IsActive { get; set; }
    
    // Navigation properties
    public virtual ApplicationUser User { get; set; }
    public virtual Tenant Tenant { get; set; }
}
```

---

## Error Codes & Constants

### Error Code Enum

```csharp
// Domain/Enums/AuthErrorCode.cs
public enum AuthErrorCode
{
    // Authentication errors
    InvalidCredentials,
    AccountLocked,
    EmailNotConfirmed,
    InvalidToken,
    TokenExpired,
    
    // Registration errors
    EmailAlreadyExists,
    RegistrationFailed,
    WeakPassword,
    
    // Email confirmation
    ConfirmationFailed,
    InvalidConfirmationToken,
    
    // Password reset
    ResetFailed,
    InvalidResetToken,
    
    // Password change
    ChangePasswordFailed,
    CurrentPasswordIncorrect,
    
    // Tenant operations
    TenantAccessDenied,
    TenantNotFound,
    
    // General
    UserNotFound,
    ValidationError,
    InternalError
}
```

### Error Response Models

```csharp
// Application/Common/Models/ErrorResponse.cs
public class ErrorResponse
{
    public ErrorDetail Error { get; set; }

    public ErrorResponse(AuthErrorCode code, string message, IEnumerable<string> errors = null)
    {
        Error = new ErrorDetail
        {
            Code = code.ToString(),
            Message = message,
            Errors = errors?.ToList()
        };
    }
}

public class ErrorDetail
{
    public string Code { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; }
}
```

---

## DbContext Configuration

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

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

        // User-Tenant relationship (many-to-many)
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

## Service Configuration

### Program.cs Setup

```csharp
// Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

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

// Configure token lifespan
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24); // Email confirmation tokens
});

// OpenIddict
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<ApplicationDbContext>();
        options.UseQuartz();
    })
    .AddServer(options =>
    {
        // Endpoints
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize")
               .SetLogoutEndpointUris("/connect/logout")
               .SetUserinfoEndpointUris("/connect/userinfo");

        // Flows
        options.AllowPasswordFlow()
               .AllowRefreshTokenFlow()
               .AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        // Scopes
        options.RegisterScopes("openid", "email", "profile", "roles", "api");

        // Certificates
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            // Production certificates
            options.AddEncryptionCertificate(LoadCertificate("CN=ETL-Encryption"));
            options.AddSigningCertificate(LoadCertificate("CN=ETL-Signing"));
        }

        // ASP.NET Core integration
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableAuthorizationEndpointPassthrough();

        // Token lifetimes
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Authentication & Authorization
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## OpenIddict Client Seeding

```csharp
// Infrastructure/Data/DbSeeder.cs
using OpenIddict.Abstractions;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();

        // Vue.js SPA Client
        if (await manager.FindByClientIdAsync("multitenant-etl-spa") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "multitenant-etl-spa",
                DisplayName = "MultiTenant ETL SPA",
                Type = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                
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
    }
}

// Call in Program.cs
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}
```

---

## Configuration Files

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=multitenanteti;Username=postgres;Password=yourpassword"
  },
  
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173"
  },
  
  "Authentication": {
    "RequireEmailConfirmation": true,
    "EmailConfirmationTokenLifetimeHours": 24,
    "PasswordResetTokenLifetimeHours": 1
  },
  
  "AzureCommunicationServices": {
    "ConnectionString": "endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your_access_key",
    "FromEmail": "DoNotReply@yourdomain.com"
  }
}
```

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  
  "Authentication": {
    "RequireEmailConfirmation": false
  }
}
```

---

## Database Migration

```bash
# Create initial migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update

# Verify tables created
# You should see: users, roles, user_roles, user_tenants, OpenIddict tables
```

---

## Testing Setup

### Verify Installation

```csharp
// Create a test endpoint
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TestController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        var canConnectToDb = _context.Database.CanConnect();
        return Ok(new 
        { 
            healthy = canConnectToDb,
            database = "Connected",
            timestamp = DateTime.UtcNow 
        });
    }
}
```

### Test Database Connection

```bash
curl http://localhost:5000/api/test/health
```

Expected response:
```json
{
  "healthy": true,
  "database": "Connected",
  "timestamp": "2025-11-20T08:17:00Z"
}
```

---

## Checklist

- [ ] All NuGet packages installed
- [ ] Database models created
- [ ] Error codes defined
- [ ] DbContext configured
- [ ] OpenIddict configured in Program.cs
- [ ] appsettings.json configured
- [ ] Database created
- [ ] Migrations run successfully
- [ ] OpenIddict clients seeded
- [ ] Health endpoint works

---

**Next:** [2. Email Service](./2-email-service.md)
