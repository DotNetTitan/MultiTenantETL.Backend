using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

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

        public AuthenticationController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context,
            IOpenIddictTokenManager tokenManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _tokenManager = tokenManager;
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

        [HttpGet("authorize"), HttpPost("authorize")]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest();

            // Retrieve the user principal from the authentication context
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

            // If the user is not authenticated, redirect to the login page
            if (!result.Succeeded)
            {
                return Challenge(
                    authenticationSchemes: IdentityConstants.ApplicationScheme,
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                            Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                    });
            }

            // Get the user from the database
            var user = await _userManager.GetUserAsync(result.Principal);
            if (user == null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer valid"
                    }));
            }

            // Ensure the user can sign in
            if (!await _signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not allowed to sign in"
                    }));
            }

            // Create a new ClaimsPrincipal containing the claims that will be used to create the authorization code or access token
            var principal = await CreateClaimsPrincipalAsync(user, request.GetScopes());

            // Automatically approve the authorization request (for implicit consent)
            // In production, you might want to show a consent screen here
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
            
            // Set the scopes in the authentication properties
            principal.SetScopes(request.GetScopes());
            
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
            var identity = (ClaimsIdentity)principal.Identity!;

            // OpenIddict required claims - using SetClaim to ensure proper setting
            identity.SetClaim(Claims.Subject, user.Id.ToString());
            identity.SetClaim(Claims.Email, user.Email);
            identity.SetClaim(Claims.Name, $"{user.FirstName} {user.LastName}");
            
            // Custom claims
            identity.SetClaim("tenant_id", user.CurrentTenantId?.ToString() ?? "");

            // Get user's roles
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            identity.SetDestinations(claim => claim.Type switch
            {
                "AspNet.Identity.SecurityStamp" => ImmutableArray<string>.Empty,
                
                Claims.Subject
                or Claims.Name
                or Claims.Email
                or ClaimTypes.Role
                or "tenant_id"
                or "tenant_name"
                    => [Destinations.AccessToken, Destinations.IdentityToken],
                
                _ => [Destinations.AccessToken]
            });

            return principal;
        }

        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke()
        {
            var request = HttpContext.GetOpenIddictServerRequest();

            // Retrieve the token from the request
            if (string.IsNullOrEmpty(request.Token))
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
    }
}
