using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.API.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _configuration = configuration;
    }

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public string FrontendUrl => _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault() 
        ?? "http://localhost:5173";

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        // If user is already authenticated, redirect back to the authorization endpoint
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return Redirect(FrontendUrl);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Find user by email
        var user = await _userManager.FindByEmailAsync(Email);
        
        if (user == null)
        {
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.LoginFailed,
                Email,
                success: false,
                errorMessage: "Invalid credentials");

            ErrorMessage = "Invalid email or password";
            return Page();
        }

        // Check if account is active
        if (!user.IsActive)
        {
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.LoginFailed,
                Email,
                success: false,
                errorMessage: "Account inactive");

            ErrorMessage = "Your account has been deactivated. Please contact your administrator.";
            return Page();
        }

        // Attempt to sign in
        var result = await _signInManager.PasswordSignInAsync(
            user,
            Password,
            RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.Login,
                Email,
                success: true);

            // Redirect back to the authorization endpoint to complete OAuth flow
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }

            return Redirect(FrontendUrl);
        }

        // Handle various failure cases
        if (result.IsLockedOut)
        {
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.LoginFailed,
                Email,
                success: false,
                errorMessage: "Account locked");

            ErrorMessage = "Your account is locked due to too many failed login attempts. Please try again later.";
            return Page();
        }

        if (result.IsNotAllowed)
        {
            await _auditService.LogAuthenticationAsync(
                Domain.Constants.AuditActions.Authentication.LoginFailed,
                Email,
                success: false,
                errorMessage: "Email not confirmed");

            ErrorMessage = "Please confirm your email address before logging in.";
            return Page();
        }

        await _auditService.LogAuthenticationAsync(
            Domain.Constants.AuditActions.Authentication.LoginFailed,
            Email,
            success: false,
            errorMessage: "Invalid credentials");

        ErrorMessage = "Invalid email or password";
        return Page();
    }
}
