using Aero.Cms.Core;
using Aero.Cms.Modules.Navigation.Areas.Api.v1;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Projections;
using Aero.Cms.Modules.Navigation.Rendering;
using Aero.Cms.Modules.Navigation.Services;
using Aero.Cms.Modules.Navigation.Validators;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Navigation;

/// <summary>
/// Registers the navigation editor, event projections, storage indexes, rendering services, and admin endpoints.
/// </summary>
[Module(nameof(NavigationModule))]
public sealed class NavigationModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(NavigationModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["content", "navigation"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["content", "navigation", "cms"];

    /// <summary>
    /// Adds scoped menu editing/context services, the singleton renderer, and request validators.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="config">The optional host configuration; this module does not read it.</param>
    /// <param name="env">The optional host environment; this module does not read it.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<INavMenuService, NavMenuService>();
        services.AddScoped<NavMenuContext>();
        services.AddSingleton<INavMenuHtmlRenderer, NavMenuHtmlRenderer>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.CreateNavigationRequest>, CreateNavigationRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.UpdateNavigationRequest>, UpdateNavigationRequestValidator>();
    }

    /// <summary>
    /// Configures inline navigation projections and the indexes that enforce site/culture/key uniqueness.
    /// </summary>
    /// <param name="opts">The AeroDB store options to mutate.</param>
public void Configure(StoreOptions opts)
    {
        opts.Projections.Add(new NavMenuDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new SiteNavigationSettingsProjection(), ProjectionLifecycle.Inline);

        opts.Schema.For<NavMenuDocument>().Identity(x => x.Id);
        opts.Schema.For<NavMenuDocument>().UseOptimisticConcurrency = true;
        opts.Schema.For<NavMenuDocument>().Index(x => x.SiteId);
        opts.Schema.For<NavMenuDocument>().Index(x => x.Culture);
        opts.Schema.For<NavMenuDocument>().Index(x => x.TranslationGroupId);
        opts.Schema.For<NavMenuDocument>().UniqueIndex(x => new { x.SiteId, x.Culture, x.Key });
        opts.Schema.For<NavMenuDocument>().Index(x => x.State);

        // DocumentAlias not available in AeroDB
        opts.Schema.For<SiteNavigationSettingsDocument>().Identity(x => x.Id);
        opts.Schema.For<SiteNavigationSettingsDocument>().UniqueIndex(x => x.SiteId);
        opts.Schema.For<SiteNavigationSettingsDocument>().Index(x => x.DefaultNavMenuId);
    }

    /// <summary>
    /// Applies the store configuration when invoked through the service-provider-aware contract.
    /// </summary>
    /// <param name="services">The host provider; it is not used by this configuration.</param>
    /// <param name="opts">The AeroDB store options to mutate.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Maps the administrative navigation endpoint group during module startup.
    /// </summary>
    /// <param name="builder">The host endpoint route builder.</param>
    /// <returns>A completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapNavigationAdminApi();
        return Task.CompletedTask;
    }
}



