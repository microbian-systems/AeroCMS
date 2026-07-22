using Aero.AppServer;
using Aero.AppServer.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Setup.Endpoints;

/// <summary>
/// Maps the bootstrap and local-infrastructure readiness endpoint used by the setup experience.
/// </summary>
public static class SetupStatusEndpoints
{
    /// <summary>
    /// Maps <c>GET /setup/status</c> and returns the supplied route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to modify.</param>
    /// <returns>The same route builder for fluent registration.</returns>
    /// <remarks>
    /// The response exposes bootstrap modes, completion flags, and readiness booleans but
    /// does not include connection strings or credentials. Missing readiness infrastructure
    /// is reported as not ready.
    /// </remarks>
public static IEndpointRouteBuilder MapSetupStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/setup/status", async (HttpContext httpContext, IServiceProvider sp, CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            var setup = sp.GetRequiredService<ISetupInitializationService>();
            var bootstrap = setup.GetBootstrapState();
            var readiness = sp.GetService<IInfrastructureReadinessSnapshot>();
            var aeroDbReady = readiness?.AeroDbReady ?? false;
            var garnetReady = readiness?.GarnetReady ?? false;
            var requiresAeroDb = string.Equals(bootstrap.DatabaseMode, "Embedded", StringComparison.OrdinalIgnoreCase);
            var requiresGarnet = string.Equals(
                bootstrap.CacheMode,
                AeroAppServerConstants.LocalCacheMode,
                StringComparison.OrdinalIgnoreCase);

            return Results.Ok(new
            {
                bootstrap.State,
                bootstrap.SetupComplete,
                bootstrap.SeedComplete,
                bootstrap.DatabaseMode,
                bootstrap.CacheMode,
                bootstrap.SecretProvider,
                bootstrap.HasBootstrapConfig,
                AeroDbReady = aeroDbReady,
                GarnetReady = garnetReady,
                RequiresAeroDb = requiresAeroDb,
                RequiresGarnet = requiresGarnet,
                IsReady = (!requiresAeroDb || aeroDbReady) && (!requiresGarnet || garnetReady)
            });
        });

        return endpoints;
    }
}

/// <summary>
/// Inserts routing and setup-status endpoint mapping into the application startup pipeline.
/// </summary>
public sealed class SetupStatusStartupFilter : IStartupFilter
{
    /// <inheritdoc />
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapSetupStatusEndpoints());
            next(app);
        };
}
