using Aero.AppServer;
using Aero.AppServer.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Setup.Endpoints;

/// <summary>
/// Represents a class for SetupStatusEndpoints.
/// </summary>
public static class SetupStatusEndpoints
{
        /// <summary>
    /// MapSetupStatusEndpoints method.
    /// </summary>
public static IEndpointRouteBuilder MapSetupStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/setup/status", async (IServiceProvider sp, CancellationToken cancellationToken) =>
        {
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
/// Represents a class for SetupStatusStartupFilter.
/// </summary>
public sealed class SetupStatusStartupFilter : IStartupFilter
{
        /// <summary>
    /// Configure method.
    /// </summary>
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapSetupStatusEndpoints());
            next(app);
        };
}
