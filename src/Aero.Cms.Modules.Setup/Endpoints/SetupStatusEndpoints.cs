using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.AppServer.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace Aero.Cms.Modules.Setup.Endpoints;

public static class SetupStatusEndpoints
{
    public static IEndpointRouteBuilder MapSetupStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/setup/status", async (IServiceProvider sp, CancellationToken cancellationToken) =>
        {
            var setup = sp.GetRequiredService<ISetupInitializationService>();
            var bootstrap = setup.GetBootstrapState();
            var readiness = sp.GetService<IInfrastructureReadinessSnapshot>();
            var postgresReady = readiness?.PostgresReady ?? false;
            var garnetReady = readiness?.GarnetReady ?? false;
            var requiresPostgres = string.Equals(bootstrap.DatabaseMode, "Embedded", StringComparison.OrdinalIgnoreCase);
            var requiresGarnet = string.Equals(bootstrap.CacheMode, "Embedded", StringComparison.OrdinalIgnoreCase);

            return Results.Ok(new
            {
                bootstrap.State,
                bootstrap.SetupComplete,
                bootstrap.SeedComplete,
                bootstrap.DatabaseMode,
                bootstrap.CacheMode,
                bootstrap.SecretProvider,
                bootstrap.HasBootstrapConfig,
                PostgresReady = postgresReady,
                GarnetReady = garnetReady,
                RequiresPostgres = requiresPostgres,
                RequiresGarnet = requiresGarnet,
                IsReady = (!requiresPostgres || postgresReady) && (!requiresGarnet || garnetReady)
            });
        });

        return endpoints;
    }
}

public sealed class SetupStatusStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapSetupStatusEndpoints());
            next(app);
        };
}
