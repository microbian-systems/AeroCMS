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

[Module(nameof(NavigationModule))]
public sealed class NavigationModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
    public override string Name => nameof(NavigationModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content", "navigation"];
    public override IReadOnlyList<string> Tags => ["content", "navigation", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<INavMenuService, NavMenuService>();
        services.AddScoped<NavMenuContext>();
        services.AddSingleton<INavMenuHtmlRenderer, NavMenuHtmlRenderer>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.CreateNavigationRequest>, CreateNavigationRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.UpdateNavigationRequest>, UpdateNavigationRequestValidator>();
    }

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

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapNavigationAdminApi();
        return Task.CompletedTask;
    }
}



