using Aero.AppServer;
using Aero.AppServer.Startup;
using System.Globalization;
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
    /// The response exposes bootstrap modes, completion flags, readiness booleans, and the
    /// non-secret site selection created by setup. It does not include connection strings or
    /// credentials. Missing readiness infrastructure is reported as not ready.
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
            SetupStateDocument? durableState = null;

            if (bootstrap.IsRunningMode
                && bootstrap.SeedComplete
                && sp.GetService<ISetupStateStore>() is { } setupStateStore)
            {
                durableState = await setupStateStore.LoadAsync(cancellationToken);
            }

            var createdSiteId = durableState?.CreatedSiteId is > 0
                ? durableState.CreatedSiteId.Value.ToString(CultureInfo.InvariantCulture)
                : null;

            if (createdSiteId is not null)
            {
                httpContext.Response.Cookies.Append("AeroCms.SiteId", createdSiteId, new CookieOptions
                {
                    Path = "/",
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    IsEssential = true,
                    Secure = httpContext.Request.IsHttps
                });
            }

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
                IsReady = (!requiresAeroDb || aeroDbReady) && (!requiresGarnet || garnetReady),
                CreatedSiteId = createdSiteId,
                SiteName = durableState?.SiteName
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
