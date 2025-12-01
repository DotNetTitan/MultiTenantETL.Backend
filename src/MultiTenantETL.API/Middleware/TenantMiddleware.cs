using MultiTenantETL.Application.Common.Interfaces;
using CustomClaims = MultiTenantETL.Domain.Constants.ClaimTypes;

namespace MultiTenantETL.API.Middleware;

/// <summary>
/// Middleware to populate TenantProvider from JWT claims for HTTP requests.
/// This ensures global query filters work correctly in web API scenarios.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
    {
        // Extract tenant ID from JWT claims
        var tenantIdClaim = context.User?.FindFirst(CustomClaims.TenantId)?.Value;
        
        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            tenantProvider.TenantId = tenantId;
        }

        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantMiddleware>();
    }
}
