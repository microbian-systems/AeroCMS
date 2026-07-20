using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Health;

/// <summary>
/// Registers ASP.NET Core health-check services and conditionally exposes the
/// aggregate health report at <c>/health</c>.
/// </summary>
/// <remarks>
/// This module does not register an <c>IHealthCheck</c> implementation and does
/// not define separate readiness or liveness probes. Outside the Development
/// environment, its endpoint executes all checks registered in the application's
/// shared health-check service collection.
/// </remarks>
[Module(nameof(HealthModule))]
public sealed class HealthModule : AeroWebModule
{
    /// <summary>The stable module identifier, <c>HealthModule</c>.</summary>
public override string Name => nameof(HealthModule);
    /// <summary>The Aero CMS version reported in module metadata.</summary>
public override string Version => AeroConstants.Version;
    /// <summary>The Aero CMS author reported in module metadata.</summary>
public override string Author => AeroConstants.Author;
    /// <summary>An empty collection because the module declares no module-ordering dependencies.</summary>
public override IReadOnlyList<string> Dependencies => [];
    /// <summary>The infrastructure and monitoring categories used to classify this module.</summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Monitoring"];
    /// <summary>The health, monitoring, and diagnostics discovery tags for this module.</summary>
public override IReadOnlyList<string> Tags => ["health", "monitoring", "diagnostics"];

    /// <summary>Registers the ASP.NET Core health-check service infrastructure.</summary>
    /// <param name="services">The application service collection to update.</param>
    /// <param name="config">Unused by this module.</param>
    /// <param name="env">Unused by this module.</param>
    /// <remarks>
    /// The call to <c>AddHealthChecks</c> does not add a concrete dependency check;
    /// checks registered elsewhere share the same service collection.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddHealthChecks();
    }

    /// <summary>Runs base module startup and maps the aggregate health endpoint outside Development.</summary>
    /// <param name="app">The endpoint builder whose service provider supplies the host environment.</param>
    /// <returns>A task that completes after endpoint registration; health checks are executed per request.</returns>
    /// <remarks>
    /// In Development, this method maps no endpoint. In other environments,
    /// <c>/health</c> uses the framework defaults: all registered checks run, the
    /// aggregate status is written as plain text, Healthy and Degraded return HTTP
    /// 200, and Unhealthy returns HTTP 503. This module attaches no explicit
    /// authorization, host restriction, CORS policy, or readiness/liveness filter;
    /// effective exposure can still be affected by the host application's pipeline
    /// and endpoint policies.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="IHostEnvironment"/> is registered in the endpoint service provider.
    /// </exception>
public override async Task RunAsync(IEndpointRouteBuilder app)
    {
        await base.RunAsync(app);

        var environment = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            app.MapHealthChecks("/health");
        }
    }
}
