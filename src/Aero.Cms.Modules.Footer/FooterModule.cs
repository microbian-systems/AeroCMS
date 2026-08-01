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
/// Registers the footer authoring, persistence, rendering, and administrative API services.
/// </summary>
/// <remarks>
/// Footer and site-default projections run inline with event persistence. Administrative endpoints
/// declare exact site read, create, update, and delete policies. The module does not configure
/// output caching or cache invalidation.
/// </remarks>
[Module(nameof(FooterModule))]
public sealed class FooterModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
    /// <inheritdoc />
    public override string Name => nameof(FooterModule);

    /// <inheritdoc />
    public override string Version => AeroConstants.Version;

    /// <inheritdoc />
    public override string Author => AeroConstants.Author;

    /// <inheritdoc />
    public override IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
    public override IReadOnlyList<string> Category => ["content", "layout"];

    /// <inheritdoc />
    public override IReadOnlyList<string> Tags => ["content", "footer", "cms"];

    /// <summary>
    /// Registers the scoped footer service and context, the singleton HTML renderer, and request validators.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="config">The host configuration. This module does not read it.</param>
    /// <param name="env">The host environment. This module does not inspect it.</param>
    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IFooterService, FooterService>();
        services.AddScoped<FooterContext>();
        services.AddSingleton<IFooterHtmlRenderer, FooterHtmlRenderer>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.CreateFooterRequest>, CreateFooterRequestValidator>();
        services.AddScoped<IValidator<Aero.Cms.Abstractions.Http.Clients.UpdateFooterRequest>, UpdateFooterRequestValidator>();
    }

    /// <summary>
    /// Configures inline event projections and indexes for footer and site-default documents.
    /// </summary>
    /// <param name="opts">The AeroDB store options to configure.</param>
    public void Configure(StoreOptions opts)
    {
        opts.Projections.Add(new FooterDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new SiteFooterSettingsProjection(), ProjectionLifecycle.Inline);

        var footers = opts.Schema.For<FooterDocument>()
            .TableName(Schemas.Tables.Footers);
        footers.Identity(x => x.Id);
        footers.UseOptimisticConcurrency = true;
        footers.Index(x => x.SiteId);
        footers.Index(x => x.Culture);
        footers.Index(x => x.TranslationGroupId);
        footers.UniqueIndex(x => new { x.SiteId, x.Culture, x.Key });
        footers.Index(x => x.State);

        var siteFooterSettings = opts.Schema.For<SiteFooterSettingsDocument>()
            .TableName(Schemas.Tables.SiteFooterSettings);
        siteFooterSettings.Identity(x => x.Id);
        siteFooterSettings.UniqueIndex(x => x.SiteId);
        siteFooterSettings.Index(x => x.DefaultFooterId);
    }

    /// <summary>
    /// Configures the footer store schema without resolving services from the provider.
    /// </summary>
    /// <param name="services">The application service provider. It is not used by this module.</param>
    /// <param name="opts">The AeroDB store options to configure.</param>
    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Maps the footer administrative endpoints.
    /// </summary>
    /// <param name="builder">The route builder that receives the endpoint group.</param>
    /// <returns>A completed task after route registration.</returns>
    /// <remarks>Each administrative endpoint declares its exact required site policy.</remarks>
    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapFooterAdminApi();
        return Task.CompletedTask;
    }
}



