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
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Footer;

/// <summary>
/// Represents a class for FooterModule.
/// </summary>
[Module(nameof(FooterModule))]
public sealed class FooterModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(FooterModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["content", "layout"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["content", "footer", "cms"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IFooterService, FooterService>();
        services.AddScoped<FooterContext>();
        services.AddSingleton<IFooterHtmlRenderer, FooterHtmlRenderer>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.CreateFooterRequest>, CreateFooterRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.UpdateFooterRequest>, UpdateFooterRequestValidator>();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions opts)
    {
        opts.Projections.Add(new FooterDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new SiteFooterSettingsProjection(), ProjectionLifecycle.Inline);

        opts.Schema.For<FooterDocument>().Identity(x => x.Id);
        opts.Schema.For<FooterDocument>().UseOptimisticConcurrency = true;
        opts.Schema.For<FooterDocument>().Index(x => x.SiteId);
        opts.Schema.For<FooterDocument>().Index(x => x.Culture);
        opts.Schema.For<FooterDocument>().Index(x => x.TranslationGroupId);
        opts.Schema.For<FooterDocument>().UniqueIndex(x => new { x.SiteId, x.Culture, x.Key });
        opts.Schema.For<FooterDocument>().Index(x => x.State);

        // DocumentAlias not available in AeroDB
        opts.Schema.For<SiteFooterSettingsDocument>().Identity(x => x.Id);
        opts.Schema.For<SiteFooterSettingsDocument>().UniqueIndex(x => x.SiteId);
        opts.Schema.For<SiteFooterSettingsDocument>().Index(x => x.DefaultFooterId);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapFooterAdminApi();
        return Task.CompletedTask;
    }
}



