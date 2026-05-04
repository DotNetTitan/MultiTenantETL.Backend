using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Infrastructure.Identity;
using MultiTenantETL.Infrastructure.Interfaces;
using System.Collections.Immutable;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("bff")]
public class BffController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClaimsService _claimsService;
    private readonly IConfiguration _configuration;

    public BffController(
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        IClaimsService claimsService,
        IConfiguration configuration)
    {
        _antiforgery = antiforgery;
        _userManager = userManager;
        _claimsService = claimsService;
        _configuration = configuration;
    }

    /// <summary>
    /// Issues/refreshes the CSRF token for the SPA.
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new
        {
            token = tokens.RequestToken
        });
    }

    /// <summary>
    /// Signs in the seeded guest demo account using secure cookie session.
    /// </summary>
    [HttpPost("guest-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GuestLogin()
    {
        var enableGuestLogin = _configuration.GetValue<bool>("Authentication:EnableGuestLogin");
        if (!enableGuestLogin)
        {
            return BadRequest(new
            {
                title = "Guest login is disabled",
                detail = "Guest login is not enabled in this environment."
            });
        }

        const string guestEmail = "guest@multitenant-etl.com";
        var guestUser = await _userManager.FindByEmailAsync(guestEmail);

        if (guestUser == null || !guestUser.IsActive)
        {
            return Unauthorized(new
            {
                title = "Guest account unavailable",
                detail = "Guest account is not available."
            });
        }

        var principal = await _claimsService.BuildClaimsPrincipalAsync(guestUser, ImmutableArray<string>.Empty);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        return Ok(new
        {
            success = true
        });
    }
}
