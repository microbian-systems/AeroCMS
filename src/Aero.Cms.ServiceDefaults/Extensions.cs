using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Aero.Cms.ServiceDefaults;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
/// <summary>
/// Provides opt-in host-builder defaults for telemetry, health checks, service discovery, and HTTP-client resilience.
/// </summary>
/// <remarks>
/// These methods register services and, when explicitly requested, map development health endpoints. They do not
/// guarantee telemetry delivery, secure endpoint exposure, service health, production readiness, or liveness.
/// Registration and mapping failures are not caught by this class.
/// </remarks>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

        /// <summary>
    /// Adds OpenTelemetry, the default health check, service discovery, and standard HTTP-client resilience defaults.
    /// </summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// Service discovery and the standard resilience handler are applied to configured HTTP clients. Allowed
    /// discovery schemes are not restricted here. This method does not map health endpoints or start exporters.
    /// </remarks>
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

        /// <summary>
    /// Registers OpenTelemetry logging, metrics, tracing, and an optional OTLP exporter.
    /// </summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// Logging includes formatted messages and scopes. Metrics cover ASP.NET Core, HTTP clients, and runtime
    /// instrumentation. Tracing adds the application-name source plus ASP.NET Core and HTTP-client instrumentation,
    /// excluding paths beginning with <c>/health</c> or <c>/alive</c>. An OTLP exporter is registered only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is non-blank; no delivery, retry, confidentiality, or collector
    /// authentication guarantee is made here.
    /// </remarks>
public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
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
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

        /// <summary>
    /// Registers an unconditional health check named <c>self</c> with the <c>live</c> tag.
    /// </summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The supplied <paramref name="builder"/>.</returns>
    /// <remarks>
    /// The check always returns healthy when invoked and does not inspect dependencies, startup completion, traffic
    /// readiness, or tenant state.
    /// </remarks>
public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

        /// <summary>
    /// Maps development-only health-check endpoints when explicitly called by the application.
    /// </summary>
    /// <param name="app">The web application to inspect and modify.</param>
    /// <returns>The supplied <paramref name="app"/>.</returns>
    /// <remarks>
    /// In the Development environment, <c>/health</c> runs all registered checks and <c>/alive</c> runs checks tagged
    /// <c>live</c>. No endpoints are mapped in other environments. This method configures no authorization and does
    /// not establish that either response is safe for untrusted exposure.
    /// </remarks>
public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
