using Aero.Cms.Core;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Aero.Cms.Abstractions.Theming;

namespace Aero.Cms.Modules.Theming;

/// <summary>
/// Registers the deployment theme catalog, stylesheet resolver, and administrative discovery endpoints.
/// </summary>
[Module(nameof(AeroThemeModule))]
public class AeroThemeModule : AeroWebModule, IConfigureAeroDB
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
        services.AddSingleton<IThemeCssCompiler, ThemeCssCompiler>();
        services.AddMemoryCache();
        services.AddScoped<ThemeDesignContextAccessor>();
        services.AddScoped<IThemeApplicationService, ThemeApplicationService>();
        services.Configure<AuthorizationOptions>(options =>
            options.AddPolicy("theme:design", policy => policy.RequireAuthenticatedUser()));
        services.AddScoped<IThemeLibrary, CompositeThemeLibrary>();
        services.AddScoped<IThemeStylesheetResolver, SiteThemeStylesheetResolver>();
    }

    /// <inheritdoc />
    public void Configure(StoreOptions options)
    {
        var definitions = options.Schema.For<ThemeDefinitionDocument>().TableName(Schemas.Tables.ThemeDefinitions).Index(x => x.TenantId).Index(x => x.Slug);
        definitions.UseOptimisticConcurrency = true;
        options.Schema.For<ThemeVersionDocument>().TableName(Schemas.Tables.ThemeVersions).Index(x => x.TenantId).Index(x => x.ThemeDefinitionId).Index(x => x.ThemeId).Index(x => x.Version);
        options.Schema.For<SiteThemePublicationDocument>().TableName(Schemas.Tables.SiteThemePublications).Index(x => x.TenantId).Index(x => x.SiteId).Index(x => x.Revision);
    }

    /// <inheritdoc />
    public void Configure(IServiceProvider services, StoreOptions options) => Configure(options);

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
