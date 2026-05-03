using Microsoft.AspNetCore.Antiforgery;

namespace MultiTenantETL.API.Middleware;

/// <summary>
/// Validates antiforgery tokens for unsafe API/BFF requests.
/// </summary>
public class ApiAntiforgeryMiddleware
{
    private readonly RequestDelegate _next;

    public ApiAntiforgeryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) ||
            HttpMethods.IsTrace(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        var isApiOrBffRequest = path.StartsWithSegments("/api") || path.StartsWithSegments("/bff");

        if (!isApiOrBffRequest)
        {
            await _next(context);
            return;
        }

        // Endpoints using form posts or OAuth protocol payloads are excluded.
        if (path.StartsWithSegments("/auth/login") ||
            path.StartsWithSegments("/connect/token") ||
            path.StartsWithSegments("/connect/revoke") ||
            path.StartsWithSegments("/bff/guest-login") ||
            path.StartsWithSegments("/api/connectors/search"))
        {
            await _next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await _next(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                status = 400,
                title = "Invalid CSRF token",
                detail = "A valid CSRF token is required for this request."
            });
        }
    }
}

public static class ApiAntiforgeryMiddlewareExtensions
{
    public static IApplicationBuilder UseApiAntiforgery(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiAntiforgeryMiddleware>();
    }
}
