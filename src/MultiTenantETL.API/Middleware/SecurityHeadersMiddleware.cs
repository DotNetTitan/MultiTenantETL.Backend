namespace MultiTenantETL.API.Middleware;

/// <summary>
/// Middleware that adds essential security headers to all HTTP responses.
/// Implements OWASP security best practices.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking attacks
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Enable browser XSS protection
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Enforce HTTPS for 1 year (31536000 seconds)
        context.Response.Headers["Strict-Transport-Security"] =
            "max-age=31536000; includeSubDomains; preload";

        // Content Security Policy - restrict resource loading
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';";

        // Referrer policy - protect user privacy
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy - disable unnecessary browser features
        context.Response.Headers["Permissions-Policy"] =
            "geolocation=(), microphone=(), camera=()";

        // Remove server identification headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding security headers middleware to the pipeline
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}