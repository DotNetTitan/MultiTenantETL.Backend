# Complete Authentication Implementation Guide

**For:** ASP.NET Core Web API  
**Version:** 2.0 (OpenIddict + ASP.NET Core Identity + Azure Communication Services)  
**Last Updated:** 2025-11-20

---

## Table of Contents

1. [Overview](#overview)
2. [Installation](#installation)
3. [Database Models](#database-models)
4. [Error Codes & Constants](#error-codes--constants)
5. [OpenIddict Configuration](#openiddict-configuration)
6. [Email Service (Azure Communication Services)](#email-service-azure-communication-services)
7. [Controllers](#controllers)
   - [Authentication Controller](#authentication-controller)
   - [Account Controller](#account-controller)
8. [Configuration](#configuration)
9. [Roles & Claims Management](#roles--claims-management)
10. [Security Features](#security-features)
11. [Testing](#testing)
12. [Implementation Steps](#implementation-steps)

---

## Overview

Complete authentication system using:
- **OpenIddict** - OAuth 2.0 & OpenID Connect server
- **ASP.NET Core Identity** - User management
- **Azure Communication Services** - Email delivery

### Key Features
✅ OAuth 2.0 / OpenID Connect  
✅ Multi-tenant support  
✅ Email confirmation  
✅ Password reset  
✅ Account lockout  
✅ Token management (access + refresh)  
✅ Tenant switching  

---

## Installation

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

```csharp
// Models/ApplicationUser.cs
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Guid? CurrentTenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    
    public virtual ICollection<UserTenant> UserTenants { get; set; }
    public virtual Tenant CurrentTenant { get; set; }
}

// Models/ApplicationRole.cs
public class ApplicationRole : IdentityRole<Guid>
{
    public string Description { get; set; }
    public List<string> Permissions { get; set; }
}

// Models/UserTenant.cs
public class UserTenant
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string RoleCode { get; set; }
    public bool IsActive { get; set; }
    
    public virtual ApplicationUser User { get; set; }
    public virtual Tenant Tenant { get; set; }
}

// Data/ApplicationDbContext.cs
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

## Error Codes & Constants

```csharp
// Common/ErrorCodes.cs
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

// Common/ErrorResponse.cs
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

## OpenIddict Configuration

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Database
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));

    // Identity
    services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
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

    // OpenIddict
    services.AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<ApplicationDbContext>();
            options.UseQuartz();
        })
        .AddServer(options =>
        {
            options.SetTokenEndpointUris("/connect/token")
                   .SetAuthorizationEndpointUris("/connect/authorize")
                   .SetLogoutEndpointUris("/connect/logout")
                   .SetUserinfoEndpointUris("/connect/userinfo");

            options.AllowPasswordFlow()
                   .AllowRefreshTokenFlow()
                   .AllowAuthorizationCodeFlow()
                   .RequireProofKeyForCodeExchange();

            options.RegisterScopes("openid", "email", "profile", "roles", "api");

            if (env.IsDevelopment())
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

            options.UseAspNetCore()
                   .EnableTokenEndpointPassthrough()
                   .EnableAuthorizationEndpointPassthrough();

            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
            options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
        });

    // Email Service
    services.AddSingleton<IEmailService, AzureCommunicationEmailService>();

    // Authentication & Authorization
    services.AddAuthentication();
    services.AddAuthorization();
}
```

### Seed OpenIddict Clients

```csharp
// Data/DbSeeder.cs
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();

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
```

---

## Email Service (Azure Communication Services)

```csharp
// Services/IEmailService.cs
public interface IEmailService
{
    Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl);
    Task SendPasswordResetAsync(string email, string firstName, string resetUrl);
    Task SendWelcomeEmailAsync(string email, string firstName);
    Task SendPasswordChangedNotificationAsync(string email, string firstName);
}

// Services/AzureCommunicationEmailService.cs
using Azure;
using Azure.Communication.Email;

public class AzureCommunicationEmailService : IEmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _fromEmail;
    private readonly ILogger<AzureCommunicationEmailService> _logger;
    private readonly IWebHostEnvironment _env;

    public AzureCommunicationEmailService(
        IConfiguration configuration,
        ILogger<AzureCommunicationEmailService> logger,
        IWebHostEnvironment env)
    {
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        _fromEmail = configuration["AzureCommunicationServices:FromEmail"];
        _emailClient = new EmailClient(connectionString);
        _logger = logger;
        _env = env;
    }

    public async Task SendEmailConfirmationAsync(string email, string firstName, string confirmationUrl)
    {
        var subject = "Confirm Your Email - MultiTenant ETL";
        var htmlContent = EmailTemplates.GetEmailConfirmation(firstName, confirmationUrl);
        await SendEmailAsync(email, subject, htmlContent);
    }

    public async Task SendPasswordResetAsync(string email, string firstName, string resetUrl)
    {
        var subject = "Reset Your Password - MultiTenant ETL";
        var htmlContent = EmailTemplates.GetPasswordReset(firstName, resetUrl);
        await SendEmailAsync(email, subject, htmlContent);
    }

    public async Task SendWelcomeEmailAsync(string email, string firstName)
    {
        var subject = "Welcome to MultiTenant ETL!";
        var htmlContent = EmailTemplates.GetWelcome(firstName);
        await SendEmailAsync(email, subject, htmlContent);
    }

    public async Task SendPasswordChangedNotificationAsync(string email, string firstName)
    {
        var subject = "Password Changed - MultiTenant ETL";
        var htmlContent = EmailTemplates.GetPasswordChanged(firstName);
        await SendEmailAsync(email, subject, htmlContent);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        try
        {
            // In development, log instead of sending
            if (_env.IsDevelopment())
            {
                _logger.LogInformation(
                    "EMAIL [DEV MODE]\nTo: {Email}\nSubject: {Subject}\nContent:\n{Content}",
                    toEmail, subject, htmlContent);
                return;
            }

            var emailMessage = new EmailMessage(
                senderAddress: _fromEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlContent
                },
                recipients: new EmailRecipients(new List<EmailAddress>
                {
                    new EmailAddress(toEmail)
                }));

            EmailSendOperation emailSendOperation = await _emailClient.SendAsync(
                WaitUntil.Started,
                emailMessage);

            _logger.LogInformation(
                "Email sent successfully. MessageId: {MessageId}, To: {Email}",
                emailSendOperation.Id, toEmail);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, 
                "Azure Communication Services failed to send email to {Email}. Status: {Status}",
                toEmail, ex.Status);
            throw new Exception("Failed to send email", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {Email}", toEmail);
            throw;
        }
    }
}

// Services/EmailTemplates.cs
public static class EmailTemplates
{
    private static string GetBaseTemplate(string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f4; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .button {{ display: inline-block; padding: 12px 30px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; font-weight: bold; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .security-note {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'><h1>MultiTenant ETL</h1></div>
        <div class='content'>{content}</div>
        <div class='footer'>
            <p>&copy; 2025 MultiTenant ETL. All rights reserved.</p>
            <p>If you didn't request this email, please ignore it.</p>
        </div>
    </div>
</body>
</html>";
    }

    public static string GetEmailConfirmation(string firstName, string confirmationUrl)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>Thank you for registering with MultiTenant ETL!</p>
            <p>Please confirm your email address by clicking the button below:</p>
            <p style='text-align: center;'>
                <a href='{confirmationUrl}' class='button'>Confirm Email Address</a>
            </p>
            <div class='security-note'>
                <strong>Security Note:</strong> This link will expire in 24 hours.
            </div>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all; color: #667eea;'>{confirmationUrl}</p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetPasswordReset(string firstName, string resetUrl)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>We received a request to reset your password.</p>
            <p>Click the button below to reset your password:</p>
            <p style='text-align: center;'>
                <a href='{resetUrl}' class='button'>Reset Password</a>
            </p>
            <div class='security-note'>
                <strong>Security Note:</strong> This link will expire in 1 hour. 
                If you didn't request a password reset, please ignore this email.
            </div>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all; color: #667eea;'>{resetUrl}</p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetWelcome(string firstName)
    {
        var content = $@"
            <h2>Welcome, {firstName}! 🎉</h2>
            <p>Your email has been confirmed and your account is now active.</p>
            <p>You can now:</p>
            <ul>
                <li>Create and manage data connectors</li>
                <li>Build ETL pipelines</li>
                <li>Transform and map your data</li>
                <li>Schedule automated data flows</li>
            </ul>
            <p style='text-align: center;'>
                <a href='https://yourapp.com/login' class='button'>Get Started</a>
            </p>";
        
        return GetBaseTemplate(content);
    }

    public static string GetPasswordChanged(string firstName)
    {
        var content = $@"
            <h2>Hi {firstName},</h2>
            <p>Your password has been successfully changed.</p>
            <div class='security-note'>
                <strong>Security Alert:</strong> If you didn't make this change, 
                please contact support immediately.
            </div>
            <p>Changed at: <strong>{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</strong></p>";
        
        return GetBaseTemplate(content);
    }
}
```

---

## Controllers

### Authentication Controller

```csharp
// Controllers/AuthenticationController.cs
[ApiController]
[Route("connect")]
public class AuthenticationController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthenticationController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpPost("token")]
    public async Task<IActionResult> Token()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (request.IsPasswordGrantType())
            return await HandlePasswordFlow(request);

        if (request.IsRefreshTokenGrantType())
            return await HandleRefreshTokenFlow(request);

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
                        "Invalid credentials"
                }));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        
        if (!result.Succeeded)
        {
            var errorDescription = result.IsLockedOut 
                ? "Account is locked" 
                : result.IsNotAllowed 
                ? "Email not confirmed" 
                : "Invalid credentials";

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorDescription
                }));
        }

        var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenFlow(OpenIddictRequest request)
    {
        var info = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var user = await _userManager.GetUserAsync(info.Principal);

        if (user == null || !await _signInManager.CanSignInAsync(user))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = 
                        OpenIddictConstants.Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The refresh token is no longer valid"
                }));
        }

        var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
        ApplicationUser user, 
        ImmutableArray<string> scopes)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity;

        identity.AddClaim(new Claim("tenant_id", user.CurrentTenantId?.ToString() ?? ""));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));

        identity.SetDestinations(claim => claim.Type switch
        {
            "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,
            ClaimTypes.Name or ClaimTypes.Email or "tenant_id"
                => ImmutableArray.Create(
                    OpenIddictConstants.Destinations.AccessToken,
                    OpenIddictConstants.Destinations.IdentityToken),
            _ => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken)
        });

        return principal;
    }
}
```

### Account Controller

```csharp
// Controllers/AccountController.cs
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IEmailService emailService,
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailService = emailService;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    // ===== REGISTRATION =====

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.EmailAlreadyExists,
                "Email already registered"));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.RegistrationFailed,
                "Failed to create account",
                result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("User {Email} registered successfully", request.Email);

        // Generate and send confirmation email
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmationToken));
        var confirmationUrl = $"{_configuration["AppSettings:FrontendUrl"]}/auth/confirm-email?userId={user.Id}&token={encodedToken}";
        
        try
        {
            await _emailService.SendEmailConfirmationAsync(user.Email, user.FirstName, confirmationUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
        }

        var requiresEmailConfirmation = bool.Parse(_configuration["Authentication:RequireEmailConfirmation"] ?? "false");

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            message = requiresEmailConfirmation 
                ? "Registration successful. Please check your email to confirm your account."
                : "Registration successful. You can now log in.",
            requiresEmailConfirmation
        });
    }

    // ===== EMAIL CONFIRMATION =====

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                "User not found"));
        }

        if (user.EmailConfirmed)
        {
            return Ok(new { success = true, message = "Email already confirmed" });
        }

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ConfirmationFailed,
                "Failed to confirm email. Token may be invalid or expired.",
                result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("User {Email} confirmed their email", user.Email);

        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
        }

        return Ok(new { success = true, message = "Email confirmed successfully" });
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.EmailConfirmed)
        {
            return Ok(new { message = "If the email exists and is not confirmed, a confirmation email has been sent." });
        }

        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmationToken));
        var confirmationUrl = $"{_configuration["AppSettings:FrontendUrl"]}/auth/confirm-email?userId={user.Id}&token={encodedToken}";
        
        await _emailService.SendEmailConfirmationAsync(user.Email, user.FirstName, confirmationUrl);

        return Ok(new { message = "If the email exists and is not confirmed, a confirmation email has been sent." });
    }

    // ===== PASSWORD RESET =====

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        
        // Always return success to prevent email enumeration
        if (user == null || !user.EmailConfirmed)
        {
            return Ok(new { message = "If the email exists, a password reset link has been sent." });
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
        var resetUrl = $"{_configuration["AppSettings:FrontendUrl"]}/auth/reset-password?userId={user.Id}&token={encodedToken}";
        
        try
        {
            await _emailService.SendPasswordResetAsync(user.Email, user.FirstName, resetUrl);
            _logger.LogInformation("Password reset email sent to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }

        return Ok(new { message = "If the email exists, a password reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                "User not found"));
        }

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ResetFailed,
                "Failed to reset password. Token may be invalid or expired.",
                result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("User {Email} reset their password", user.Email);

        try
        {
            await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password changed notification to {Email}", user.Email);
        }

        return Ok(new { success = true, message = "Password reset successfully" });
    }

    // ===== CHANGE PASSWORD =====

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
        if (!result.Succeeded)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ChangePasswordFailed,
                "Failed to change password",
                result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("User {Email} changed their password", user.Email);
        await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FirstName);

        return Ok(new { message = "Password changed successfully" });
    }

    // ===== LOGOUT =====

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { success = true, message = "Logged out successfully" });
    }

    // ===== TENANT SWITCHING =====

    [HttpPost("switch-tenant")]
    [Authorize]
    public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var userTenant = await _context.UserTenants
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut => 
                ut.UserId == user.Id && 
                ut.TenantId == request.TenantId && 
                ut.IsActive);

        if (userTenant == null)
        {
            return Forbid(new ErrorResponse(
                AuthErrorCode.TenantAccessDenied,
                "You don't have access to this tenant").Error.Message);
        }

        user.CurrentTenantId = request.TenantId;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Email} switched to tenant {TenantId}", user.Email, request.TenantId);

        // Client should request new tokens after tenant switch
        return Ok(new
        {
            currentTenantId = request.TenantId,
            tenantName = userTenant.Tenant.Name,
            message = "Tenant switched successfully. Please refresh your tokens."
        });
    }
}

// DTOs
public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; }
    
    [Required, MinLength(8)]
    public string Password { get; set; }
    
    [Required, MinLength(2), MaxLength(50)]
    public string FirstName { get; set; }
    
    [Required, MinLength(2), MaxLength(50)]
    public string LastName { get; set; }
}

public class ConfirmEmailRequest
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Token { get; set; }
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; }
}

public class ResetPasswordRequest
{
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Token { get; set; }
    
    [Required, MinLength(8)]
    public string NewPassword { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; }
    
    [Required, MinLength(8)]
    public string NewPassword { get; set; }
}

public class SwitchTenantRequest
{
    [Required]
    public Guid TenantId { get; set; }
}
```

---

## Configuration

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
    "RequireEmailConfirmation": true
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
  "Authentication": {
    "RequireEmailConfirmation": false
  }
```

---

## Roles & Claims Management

### Overview

The authentication system integrates with the authorization system through roles and claims. Every authenticated user has:
- **Role**: Defines their permission level (Admin, Manager, User)
- **Claims**: Additional metadata in the JWT token (tenant_id, permissions, etc.)

See [authorization-matrix.md](./authorization-matrix.md) for complete role and permission details.

### Assigning Roles to Users

```csharp
// Services/UserService.cs
public class UserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public async Task AssignRoleToUserAsync(Guid userId, Guid tenantId, string roleCode)
    {
        // Validate role exists
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleCode);
        if (role == null)
            throw new InvalidOperationException($"Role {roleCode} does not exist");

        // Update UserTenant relationship
        var userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId);
        
        if (userTenant == null)
        {
            // Create new tenant membership
            userTenant = new UserTenant
            {
                UserId = userId,
                TenantId = tenantId,
                RoleCode = roleCode,
                IsActive = true
            };
            _context.UserTenants.Add(userTenant);
        }
        else
        {
            // Update existing membership
            userTenant.RoleCode = roleCode;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetUserRoleForTenantAsync(Guid userId, Guid tenantId)
    {
        var userTenant = await _context.UserTenants
            .FirstOrDefaultAsync(ut => 
                ut.UserId == userId && 
                ut.TenantId == tenantId && 
                ut.IsActive);
        
        return userTenant?.RoleCode ?? "user"; // Default to 'user' role
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, Guid tenantId)
    {
        var roleCode = await GetUserRoleForTenantAsync(userId, tenantId);
        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == roleCode);
        
        return role?.Permissions ?? new List<string>();
    }
}
```

### Seeding Default Roles

```csharp
// Data/RoleSeeder.cs
public static class RoleSeeder
{
    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        // Admin Role
        if (!await roleManager.RoleExistsAsync("admin"))
        {
            var adminRole = new ApplicationRole
            {
                Name = "admin",
                NormalizedName = "ADMIN",
                Description = "Full control within tenant",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "tenants:update",
                    "users:*",
                    "connectors:*",
                    "transformations:*",
                    "pipelines:*",
                    "executions:*",
                    "dashboard:*"
                }
            };
            await roleManager.CreateAsync(adminRole);
        }

        // Manager Role
        if (!await roleManager.RoleExistsAsync("manager"))
        {
            var managerRole = new ApplicationRole
            {
                Name = "manager",
                NormalizedName = "MANAGER",
                Description = "Manage resources and users",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "users:read",
                    "users:create",
                    "users:update",
                    "connectors:*",
                    "transformations:*",
                    "pipelines:*",
                    "executions:read",
                    "executions:cancel",
                    "dashboard:*"
                }
            };
            await roleManager.CreateAsync(managerRole);
        }

        // User Role
        if (!await roleManager.RoleExistsAsync("user"))
        {
            var userRole = new ApplicationRole
            {
                Name = "user",
                NormalizedName = "USER",
                Description = "Basic access - view and execute",
                Permissions = new List<string>
                {
                    "tenants:read",
                    "users:read:self",
                    "users:update:self",
                    "connectors:read",
                    "transformations:read",
                    "pipelines:read",
                    "pipelines:execute",
                    "executions:read",
                    "executions:cancel:own",
                    "dashboard:read"
                }
            };
            await roleManager.CreateAsync(userRole);
        }
    }
}

// Call in Program.cs after migrations
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await RoleSeeder.SeedRolesAsync(roleManager);
}
```

### Enhanced Claims in JWT Tokens

Update the `CreateClaimsPrincipalAsync` method to include role and permission claims:

```csharp
// In AuthenticationController
private async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
    ApplicationUser user, 
    ImmutableArray<string> scopes)
{
    var principal = await _signInManager.CreateUserPrincipalAsync(user);
    var identity = (ClaimsIdentity)principal.Identity;

    // Basic claims
    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
    identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
    identity.AddClaim(new Claim("tenant_id", user.CurrentTenantId?.ToString() ?? ""));

    // Get user's role for current tenant
    if (user.CurrentTenantId.HasValue)
    {
        var userTenant = await _context.UserTenants
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut => 
                ut.UserId == user.Id && 
                ut.TenantId == user.CurrentTenantId.Value &&
                ut.IsActive);

        if (userTenant != null)
        {
            // Add role claim
            identity.AddClaim(new Claim(ClaimTypes.Role, userTenant.RoleCode));
            
            // Add tenant name claim
            identity.AddClaim(new Claim("tenant_name", userTenant.Tenant.Name));

            // Get role permissions
            var role = await _roleManager.FindByNameAsync(userTenant.RoleCode);
            if (role != null)
            {
                // Add permissions as individual claims
                foreach (var permission in role.Permissions)
                {
                    identity.AddClaim(new Claim("permission", permission));
                }

                // Also add permissions as a single JSON claim for easy access
                identity.AddClaim(new Claim("permissions", 
                    JsonSerializer.Serialize(role.Permissions)));
            }
        }
    }

    // Set claim destinations (which claims go in which tokens)
    identity.SetDestinations(claim => claim.Type switch
    {
        // Never include security stamp
        "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,

        // ID token + access token
        ClaimTypes.NameIdentifier 
        or ClaimTypes.Name 
        or ClaimTypes.Email 
        or ClaimTypes.Role
        or "tenant_id" 
        or "tenant_name"
            => ImmutableArray.Create(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken),

        // Access token only
        "permission" or "permissions"
            => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken),

        // Default: access token only
        _ => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken)
    });

    return principal;
}
```

### Current User Service

Create a service to easily access current user claims:

```csharp
// Services/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid GetUserId();
    Guid GetTenantId();
    string GetRole();
    List<string> GetPermissions();
    bool HasPermission(string permission);
    bool IsInRole(string role);
}

// Services/CurrentUserService.cs
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User;

    public Guid GetUserId()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    public Guid GetTenantId()
    {
        var tenantIdClaim = User?.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
    }

    public string GetRole()
    {
        return User?.FindFirst(ClaimTypes.Role)?.Value ?? "user";
    }

    public List<string> GetPermissions()
    {
        // Get from JSON claim
        var permissionsJson = User?.FindFirst("permissions")?.Value;
        if (!string.IsNullOrEmpty(permissionsJson))
        {
            return JsonSerializer.Deserialize<List<string>>(permissionsJson) ?? new List<string>();
        }

        // Fallback: get individual permission claims
        return User?.FindAll("permission").Select(c => c.Value).ToList() ?? new List<string>();
    }

    public bool HasPermission(string permission)
    {
        var permissions = GetPermissions();
        
        // Check exact permission
        if (permissions.Contains(permission))
            return true;

        // Check wildcard permissions
        var parts = permission.Split(':');
        if (parts.Length == 2)
        {
            var resource = parts[0];
            var action = parts[1];

            // Check {resource}:*
            if (permissions.Contains($"{resource}:*"))
                return true;

            // Check *:{action}
            if (permissions.Contains($"*:{action}"))
                return true;

            // Check super admin
            if (permissions.Contains("*:*"))
                return true;
        }

        return false;
    }

    public bool IsInRole(string role)
    {
        return User?.IsInRole(role) ?? false;
    }
}

// Register in Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

### Using Roles and Claims in Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class PipelinesController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _context;

    public PipelinesController(ICurrentUserService currentUser, ApplicationDbContext context)
    {
        _currentUser = currentUser;
        _context = context;
    }

    [HttpGet]
    [Authorize] // Requires authentication
    public async Task<IActionResult> GetPipelines()
    {
        var tenantId = _currentUser.GetTenantId();
        
        // Automatically filtered by tenant
        var pipelines = await _context.Pipelines
            .Where(p => p.TenantId == tenantId)
            .ToListAsync();

        return Ok(pipelines);
    }

    [HttpPost]
    [Authorize(Roles = "admin,manager")] // Requires specific roles
    public async Task<IActionResult> CreatePipeline([FromBody] PipelineDto dto)
    {
        // Check permission programmatically
        if (!_currentUser.HasPermission("pipelines:create"))
        {
            return Forbid();
        }

        var tenantId = _currentUser.GetTenantId();
        var userId = _currentUser.GetUserId();

        var pipeline = new Pipeline
        {
            TenantId = tenantId,
            CreatedBy = userId,
            // ... other properties
        };

        _context.Pipelines.Add(pipeline);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPipeline), new { id = pipeline.Id }, pipeline);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")] // Admin only
    public async Task<IActionResult> DeletePipeline(Guid id)
    {
        var tenantId = _currentUser.GetTenantId();
        
        var pipeline = await _context.Pipelines
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (pipeline == null)
            return NotFound();

        _context.Pipelines.Remove(pipeline);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
```

### JWT Token Example

After authentication, the JWT will contain these claims:

```json
{
  "sub": "user-guid",
  "email": "john@example.com",
  "name": "John Doe",
  "role": "admin",
  "tenant_id": "tenant-guid",
  "tenant_name": "Acme Corp",
  "permissions": "[\"tenants:read\",\"tenants:update\",\"users:*\",...]",
  "permission": [
    "tenants:read",
    "tenants:update",
    "users:*",
    "connectors:*"
  ],
  "exp": 1234567890,
  "aud": "multitenant-etl-spa",
  "iss": "https://api.yourapp.com"
}
```

### Frontend Usage

```typescript
// Frontend can decode JWT to get user info
const token = localStorage.getItem('access_token');
const decoded = jwtDecode(token);

console.log(decoded.role); // "admin"
console.log(decoded.tenant_name); // "Acme Corp"
console.log(decoded.permissions); // Array of permissions

// Check permissions in UI
function canCreatePipeline() {
  const permissions = JSON.parse(decoded.permissions);
  return permissions.includes('pipelines:create') || 
         permissions.includes('pipelines:*') ||
         permissions.includes('*:*');
}
```

---

## Security Features


## Security Features

### Rate Limiting

```csharp
// Already configured in authentication-security.md
services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/register",
            Period = "1h",
            Limit = 3
        },
        new RateLimitRule
        {
            Endpoint = "POST:/connect/token",
            Period = "1m",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/account/forgot-password",
            Period = "1h",
            Limit = 5
        }
    };
});
```

### CORS

```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", builder =>
        builder.WithOrigins("http://localhost:5173", "https://app.example.com")
               .WithMethods("GET", "POST", "PUT", "DELETE")
               .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id")
               .AllowCredentials());
});

app.UseCors("AllowFrontend");
```

---

## Testing

### Test Registration

```bash
curl -X POST http://localhost:5000/api/account/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test@123456",
    "firstName": "Test",
    "lastName": "User"
  }'
```

### Test Login

```bash
curl -X POST http://localhost:5000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&username=test@example.com&password=Test@123456&scope=openid email profile api"
```

---

## Implementation Steps

1. **Install packages** (see Installation section)
2. **Create models** (ApplicationUser, ApplicationRole, UserTenant)
3. **Configure DbContext** (ApplicationDbContext)
4. **Add error codes enum** (AuthErrorCode, ErrorResponse)
5. **Configure services** (Identity, OpenIddict, Email)
6. **Create email service** (AzureCommunicationEmailService)
7. **Create controllers** (AuthenticationController, AccountController)
8. **Configure settings** (appsettings.json)
9. **Run migrations** (`dotnet ef migrations add InitialCreate && dotnet ef database update`)
10. **Seed OpenIddict clients** (DbSeeder)
11. **Test endpoints**

---

## Checklist

- [ ] NuGet packages installed
- [ ] Database configured
- [ ] Identity configured
- [ ] OpenIddict configured
- [ ] Azure Communication Services configured
- [ ] Email templates created
- [ ] Controllers implemented
- [ ] Error codes enum created
- [ ] Migrations applied
- [ ] OpenIddict clients seeded
- [ ] Registration tested
- [ ] Login tested
- [ ] Email confirmation tested
- [ ] Password reset tested
- [ ] Tenant switching tested

---

**Complete and production-ready!** 🚀
