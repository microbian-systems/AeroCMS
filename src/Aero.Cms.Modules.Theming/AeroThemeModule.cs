using Aero.Cms.Core;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Abstractions.Theming;

namespace Aero.Cms.Modules.Theming;

/// <summary>
/// Registers the deployment theme catalog, stylesheet resolver, and administrative discovery endpoints.
/// </summary>
[Module(nameof(AeroThemeModule))]
public class AeroThemeModule : AeroWebModule
{
    /// <inheritdoc />
public override string Name { get; } = nameof(AeroThemeModule);
    /// <inheritdoc />
public override string Version { get; } = AeroConstants.Version;
    /// <inheritdoc />
public override string Author { get; } = AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category { get; } = ["theme", "themes", "ui"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags { get; } = ["themes", "theme", "ui"];

    /// <inheritdoc />
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddHttpContextAccessor();
        services.AddSingleton<IThemeCatalog>(new DeploymentThemeCatalog([BuiltInThemeManifest.Create()]));
        services.AddScoped<IThemeStylesheetResolver, SiteThemeStylesheetResolver>();
    }

    /// <summary>
    /// Maps theme endpoints during module startup.
    /// </summary>
    /// <param name="builder">The host endpoint route builder.</param>
    /// <returns>A completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapThemesApi();
        return Task.CompletedTask;
    }
}
