using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for adding default Aspire service configurations.
/// </summary>
public static class Extensions
{
    private const string OtlpExporterEndpointEnvVar = "OTEL_EXPORTER_OTLP_ENDPOINT";
    /// <summary>
    /// Adds service defaults to the host application builder, including OpenTelemetry,
    /// health checks, and service discovery.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> for chaining.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for the application with logging, metrics, and tracing.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> for chaining.</returns>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpExporterEndpointEnvVar]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Adds default health checks to the application.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/>.</param>
    /// <returns>The <see cref="IHostApplicationBuilder"/> for chaining.</returns>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps default health check endpoints for the application.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/>.</param>
    /// <returns>The <see cref="WebApplication"/> for chaining.</returns>
    /// <remarks>
    /// Health check endpoints are only mapped in development environments by default.
    /// Adding health checks endpoints in non-development environments has security implications.
    /// See https://aka.ms/dotnet/aspire/healthchecks for details before enabling in production.
    /// </remarks>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
