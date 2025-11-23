using System.Collections.Immutable;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Authentication.Models;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using MultiTenantETL.Infrastructure.Interfaces;

namespace MultiTenantETL.API.Controllers
{
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
        private readonly IOpenIddictScopeManager _scopeManager;
        private readonly IOpenIddictTokenManager _tokenManager;
        private readonly ITenantService _tenantService;
        private readonly IClaimsService _claimsService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IConfiguration configuration,
            ApplicationDbContext context,
            ILogger<AccountController> logger,
            IOpenIddictScopeManager scopeManager,
            IOpenIddictTokenManager tokenManager,
            ITenantService tenantService,
            IClaimsService claimsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _scopeManager = scopeManager;
            _tokenManager = tokenManager;
            _tenantService = tenantService;
            _claimsService = claimsService;
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

            // Create a default personal tenant for the user
            var tenantSlug = $"user-{user.Id.ToString()[..8]}";
            var tenantName = $"{user.FirstName}'s Workspace";
            var tenantResult = await _tenantService.CreateTenantAsync(tenantName, tenantSlug);

            if (tenantResult.Success)
            {
                // Add user to their new tenant with TenantAdmin role
                await _tenantService.AddUserToTenantAsync(
                    user.Id,
                    tenantResult.Data!.Id,
                    Domain.Constants.Roles.TenantAdmin);

                _logger.LogInformation(
                    "Created default tenant {TenantId} for user {Email}",
                    tenantResult.Data.Id,
                    request.Email);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to create default tenant for user {Email}: {Error}",
                    request.Email,
                    tenantResult.ErrorMessage);
            }

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
            
            // Revoke all tokens for security - user must re-authenticate
            await RevokeUserTokensAsync(user.Id);
            _logger.LogInformation("Revoked all tokens for user {Email} after password change", user.Email);
            
            await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FirstName);

            return Ok(new { message = "Password changed successfully. Please log in again with your new password." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Revoke all refresh tokens for this user
                await RevokeUserTokensAsync(user.Id);
                _logger.LogInformation("Revoked all tokens for user {Email}", user.Email);
            }
            
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out");
            
            return Ok(new { success = true, message = "Logged out successfully. All refresh tokens have been revoked." });
        }

        [HttpPost("switch-tenant")]
        [Authorize]
        public async Task<IActionResult> SwitchTenant([FromBody] SwitchTenantRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            // Use tenant service to switch tenant
            var result = await _tenantService.SwitchUserTenantAsync(user.Id, request.TenantId);
            
            if (!result.Success)
            {
                return BadRequest(new ErrorResponse(result.ErrorCode!.Value, result.ErrorMessage!));
            }

            _logger.LogInformation("User {Email} switched to tenant {TenantId}", user.Email, request.TenantId);

            // Use ClaimsService to build new principal with updated tenant
            var principal = await _claimsService.BuildClaimsPrincipalAsync(user, ImmutableArray<string>.Empty);

            // Sign in with new claims to generate new token
            var authProperties = new AuthenticationProperties();
            await HttpContext.SignInAsync(
                OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                principal,
                authProperties);

            return Ok(new
            {
                currentTenantId = request.TenantId,
                tenantName = result.UserTenant!.Tenant.Name,
                message = "Tenant switched successfully. Use your current refresh token to get a new access token with updated tenant."
            });
        }

        private async Task RevokeUserTokensAsync(Guid userId)
        {
            // Get all tokens for the user
            var tokens = _tokenManager.FindBySubjectAsync(userId.ToString());

            // Revoke each token
            await foreach (var token in tokens)
            {
                await _tokenManager.TryRevokeAsync(token);
            }
        }
    }
}
