using Aero.Cms.Core;
using Aero.Cms.Modules.Footer.Areas.Api.v1;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Projections;
using Aero.Cms.Modules.Footer.Rendering;
using Aero.Cms.Modules.Footer.Services;
using Aero.Cms.Modules.Footer.Validators;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using JasperFx.Events.Projections;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Footer;

[Module(nameof(FooterModule))]
public sealed class FooterModule : AeroWebModule, IUiModule, IConfigureMarten
{
    public override string Name => nameof(FooterModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content", "layout"];
    public override IReadOnlyList<string> Tags => ["content", "footer", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IFooterService, FooterService>();
        services.AddScoped<FooterContext>();
        services.AddSingleton<IFooterHtmlRenderer, FooterHtmlRenderer>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.CreateFooterRequest>, CreateFooterRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.UpdateFooterRequest>, UpdateFooterRequestValidator>();
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Projections.Add(new FooterDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new SiteFooterSettingsProjection(), ProjectionLifecycle.Inline);

        opts.Schema.For<FooterDocument>().DocumentAlias("footers");
        opts.Schema.For<FooterDocument>().Identity(x => x.Id);
        opts.Schema.For<FooterDocument>().UseOptimisticConcurrency(true);
        opts.Schema.For<FooterDocument>().Index(x => x.SiteId);
        opts.Schema.For<FooterDocument>().Index(x => x.Culture);
        opts.Schema.For<FooterDocument>().Index(x => x.TranslationSetId);
        opts.Schema.For<FooterDocument>().UniqueIndex(x => x.SiteId, x => x.Culture, x => x.Key);
        opts.Schema.For<FooterDocument>().Index(x => x.State);
        Configure<FooterDocument>(services, opts);

        opts.Schema.For<SiteFooterSettingsDocument>().DocumentAlias("site_footer_settings");
        opts.Schema.For<SiteFooterSettingsDocument>().Identity(x => x.Id);
        opts.Schema.For<SiteFooterSettingsDocument>().UniqueIndex(x => x.SiteId);
        opts.Schema.For<SiteFooterSettingsDocument>().Index(x => x.DefaultFooterId);
        Configure<SiteFooterSettingsDocument>(services, opts);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapFooterAdminApi();
        return Task.CompletedTask;
    }
}
