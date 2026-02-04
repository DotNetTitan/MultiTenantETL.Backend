# 3. Controllers

**Purpose:** Authentication and Account management endpoints

---

## DTOs

```csharp
// Application/Authentication/Models/AuthDtos.cs
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

## Authentication Controller

```csharp
// API/Controllers/AuthenticationController.cs
[ApiController]
[Route("connect")]
public class AuthenticationController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public AuthenticationController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
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

        // Basic claims
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim("tenant_id", user.CurrentTenantId?.ToString() ?? ""));

        // Get user's role and permissions for current tenant
        // See file 4-roles-claims.md for complete implementation

        identity.SetDestinations(claim => claim.Type switch
        {
            "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,
            ClaimTypes.NameIdentifier 
            or ClaimTypes.Name 
            or ClaimTypes.Email 
            or ClaimTypes.Role
            or "tenant_id" 
            or "tenant_name"
                => ImmutableArray.Create(
                    OpenIddictConstants.Destinations.AccessToken,
                    OpenIddictConstants.Destinations.IdentityToken),
            _ => ImmutableArray.Create(OpenIddictConstants.Destinations.AccessToken)
        });

        return principal;
    }
}
```

---

## Account Controller

```csharp
// API/Controllers/AccountController.cs
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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { success = true, message = "Logged out successfully" });
    }

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

        return Ok(new
        {
            currentTenantId = request.TenantId,
            tenantName = userTenant.Tenant.Name,
            message = "Tenant switched successfully. Please refresh your tokens."
        });
    }
}
```

---

## Checklist

- [ ] DTOs created
- [ ] AuthenticationController implemented
- [ ] AccountController implemented
- [ ] All endpoints tested
- [ ] Error handling verified

---

**Next:** [4. Roles & Claims](./4-roles-claims.md)
