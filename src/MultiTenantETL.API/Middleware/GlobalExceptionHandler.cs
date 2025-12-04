using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Exceptions;
using ValidationException = MultiTenantETL.Application.Common.Exceptions.ValidationException;

namespace MultiTenantETL.API.Middleware;

/// <summary>
/// Global exception handler that provides consistent error responses using ProblemDetails.
/// Implements IExceptionHandler for ASP.NET Core 8.0+ exception handling.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Handle FluentValidation exceptions specially
        if (exception is FluentValidation.ValidationException fluentValidationException)
        {
            await HandleFluentValidationException(httpContext, fluentValidationException, cancellationToken);
            return true;
        }

        // Handle custom ValidationException
        if (exception is ValidationException validationException)
        {
            await HandleCustomValidationException(httpContext, validationException, cancellationToken);
            return true;
        }

        var (statusCode, title, detail) = MapException(exception);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            httpContext.TraceIdentifier,
            httpContext.Request.Path,
            httpContext.Request.Method);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = _environment.IsDevelopment() ? detail : null,
            Instance = httpContext.Request.Path,
            Type = GetProblemType(statusCode)
        };

        // Add trace ID for correlation
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        // Include exception details in development
        if (_environment.IsDevelopment() && exception.StackTrace != null)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private async Task HandleFluentValidationException(
        HttpContext httpContext, 
        FluentValidation.ValidationException exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Validation error occurred. TraceId: {TraceId}, Path: {Path}, Errors: {Errors}",
            httpContext.TraceIdentifier,
            httpContext.Request.Path,
            exception.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));

        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Instance = httpContext.Request.Path,
            Type = GetProblemType((int)HttpStatusCode.BadRequest)
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private async Task HandleCustomValidationException(
        HttpContext httpContext,
        ValidationException exception,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Validation error occurred. TraceId: {TraceId}, Path: {Path}",
            httpContext.TraceIdentifier,
            httpContext.Request.Path);

        var problemDetails = new ValidationProblemDetails(exception.Errors)
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Validation Failed",
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Type = GetProblemType((int)HttpStatusCode.BadRequest)
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            // Not Found
            KeyNotFoundException ex => (
                (int)HttpStatusCode.NotFound,
                "Resource Not Found",
                ex.Message),

            // Bad Request / Validation
            ArgumentNullException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Request",
                ex.Message),

            ArgumentException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Argument",
                ex.Message),

            FormatException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Format",
                ex.Message),

            // Conflict
            InvalidOperationException ex when ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) => (
                (int)HttpStatusCode.Conflict,
                "Resource Conflict",
                ex.Message),

            // Authorization
            UnauthorizedAccessException ex => (
                (int)HttpStatusCode.Forbidden,
                "Access Denied",
                ex.Message),

            // Operation Cancelled
            OperationCanceledException => (
                499, // Client Closed Request
                "Request Cancelled",
                "The request was cancelled by the client."),

            // Timeout
            TimeoutException ex => (
                (int)HttpStatusCode.GatewayTimeout,
                "Operation Timeout",
                ex.Message),

            // Other InvalidOperationException
            InvalidOperationException ex => (
                (int)HttpStatusCode.BadRequest,
                "Invalid Operation",
                ex.Message),

            // Default - Internal Server Error
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Internal Server Error",
                exception.Message)
        };
    }

    private static string GetProblemType(int statusCode)
    {
        return statusCode switch
        {
            400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            401 => "https://tools.ietf.org/html/rfc7235#section-3.1",
            403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            499 => "https://httpstatuses.com/499",
            500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            504 => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }
}
