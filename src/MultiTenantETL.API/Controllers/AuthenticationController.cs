using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.API.Controllers
{
    [ApiController]
    [Route("connect")]
    public class AuthenticationController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IOpenIddictTokenManager _tokenManager;
        private readonly IClaimsService _claimsService;
        private readonly IAuditService _auditService;

        public AuthenticationController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context,
            IOpenIddictTokenManager tokenManager,
            IClaimsService claimsService,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _tokenManager = tokenManager;
            _claimsService = claimsService;
            _auditService = auditService;
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (request.IsPasswordGrantType())
                return await HandlePasswordFlow(request);

            if (request.IsAuthorizationCodeGrantType())
                return await HandleAuthorizationCodeFlow(request);

            if (request.IsRefreshTokenGrantType())
                return await HandleRefreshTokenFlow(request);

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                        OpenIddictConstants.Errors.UnsupportedGrantType,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The specified grant type is not supported by this authorization server."
                }));
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            var request = HttpContext.GetOpenIddictServerRequest();

            // Retrieve the token from the request
            if (request == null || string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new
                {
                    error = OpenIddictConstants.Errors.InvalidRequest,
                    error_description = "The token parameter is missing."
                });
            }

            // Find the token in the database
            var token = await _tokenManager.FindByIdAsync(request.Token);
            if (token == null)
            {
                // Token not found - this is not an error per RFC 7009
                return Ok();
            }

            // Revoke the token and any associated tokens (e.g., refresh tokens)
            await _tokenManager.TryRevokeAsync(token);

            return Ok();
        }

        [HttpGet("authorize"), HttpPost("authorize")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // Check if prompt=login is requested (forces re-authentication)
            var prompt = request.GetParameter("prompt")?.ToString();
            var forceLogin = prompt == "login";

            // Standard OAuth flow: check if user is already authenticated via cookie
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result.Succeeded && result.Principal != null && !forceLogin)
            {
                var user = await _userManager.GetUserAsync(result.Principal);
                if (user != null && await _signInManager.CanSignInAsync(user) && user.IsActive)
                {
                    var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());
                    principal.SetScopes(request.GetScopes());
                    return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }
                else
                {
                    // User exists in cookie but is no longer valid - clear the stale authentication
                    await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            }

            // If forceLogin is requested, sign out first
            if (forceLogin && result.Succeeded)
            {
                await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }

            // User is not authenticated - redirect to login page
            // Store the current authorization request URL so we can return to it after login
            var returnUrl = HttpContext.Request.PathBase + HttpContext.Request.Path + HttpContext.Request.QueryString;
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        private async Task<IActionResult> HandlePasswordFlow(OpenIddictRequest request)
        {
            var user = await _userManager.FindByNameAsync(request.Username!) ??
                       await _userManager.FindByEmailAsync(request.Username!);

            if (user == null)
            {
                // Audit failed login attempt
                await _auditService.LogAuthenticationAsync(
                    Domain.Constants.AuditActions.Authentication.LoginFailed,
                    request.Username,
                    success: false,
                    errorMessage: "Invalid credentials");

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Invalid credentials"
                    }));
            }

            // Check if user account is active
            if (!user.IsActive)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Account is inactive. Please contact your administrator."
                    }));
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                var errorDescription = result.IsLockedOut
                    ? "Account is locked"
                    : result.IsNotAllowed
                    ? "Email not confirmed"
                    : "Invalid credentials";

                // Audit failed login attempt
                await _auditService.LogAuthenticationAsync(
                    Domain.Constants.AuditActions.Authentication.LoginFailed,
                    user.Email,
                    success: false,
                    errorMessage: errorDescription);

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = errorDescription
                    }));
            }

            var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());
            
            // Set the scopes in the authentication properties
            principal.SetScopes(request.GetScopes());

            // Audit successful login
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.Login,
                user.Email,
                success: true);
            
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        private async Task<IActionResult> HandleAuthorizationCodeFlow(OpenIddictRequest request)
        {
            // Retrieve the claims principal stored in the authorization code
            var info = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            // Retrieve the user from the database
            var user = info.Principal != null ? await _userManager.GetUserAsync(info.Principal) : null;
            if (user == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The authorization code is no longer valid"
                    }));
            }

            // Ensure the user is still allowed to sign in
            if (!await _signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not allowed to sign in"
                    }));
            }

            // Check if user account is still active
            if (!user.IsActive)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Account is inactive"
                    }));
            }

            // Create a new claims principal with fresh claims
            var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());

            // Return the access token
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        private async Task<IActionResult> HandleRefreshTokenFlow(OpenIddictRequest request)
        {
            var info = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var user = info.Principal != null ? await _userManager.GetUserAsync(info.Principal) : null;

            if (user == null || !await _signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
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
            return await _claimsService.BuildClaimsPrincipalAsync(user, scopes);
        }
    }
}