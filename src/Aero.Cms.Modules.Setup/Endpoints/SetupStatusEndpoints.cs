using Aero.Cms.Modules.Setup.Bootstrap;
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
            var readinessType = Type.GetType("Aero.AppServer.Startup.InfrastructureReadinessSnapshot, Aero.AppServer");
            var readiness = readinessType is null ? null : sp.GetService(readinessType);
            var bootstrap = setup.GetBootstrapState();

            bool GetBool(string name)
                => readiness?.GetType().GetProperty(name)?.GetValue(readiness) as bool? ?? false;

            return Results.Ok(new
            {
                bootstrap.SetupComplete,
                bootstrap.SeedComplete,
                bootstrap.DatabaseMode,
                bootstrap.CacheMode,
                bootstrap.SecretProvider,
                bootstrap.HasBootstrapConfig,
                PostgresReady = GetBool("PostgresReady"),
                GarnetReady = GetBool("GarnetReady")
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
