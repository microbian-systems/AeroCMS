using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;
using Aero.Core.Http;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using FluentValidation;
using Aero.Cms.Html;
using Aero.Cms.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Registers page APIs, Razor routes, hierarchy services, HTML rendering, and
/// Sable schemas for the Pages module.
/// </summary>
[Module(nameof(PagesModule))]
public sealed class PagesModule : AeroWebModule, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(PagesModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => ["SitesModule"];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["content", "pages"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["content", "pages", "cms"];


    /// <summary>
    /// Registers site-scoped page services, the singleton page actor, HTML tooling,
    /// validation, and public/preview Razor Page conventions.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">The host configuration; this implementation does not read it.</param>
    /// <param name="env">The host environment; this implementation does not read it.</param>
    /// <remarks>
    /// The scoped content-service factory captures the current site and user name at
    /// the HTTP boundary. Outside an authenticated HTTP request, the audit actor is
    /// <c>system</c>.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Content service — factory resolves ISiteContext + IHttpContextAccessor
        // at the boundary and converts them to explicit primitives so the service
        // never touches HTTP transport concerns.
        services.AddScoped<IPageContentService>(sp =>
        {
            var session = sp.GetRequiredService<IDocumentSession>();
            var bus = sp.GetRequiredService<IMessageBus>();
            var siteContext = sp.GetRequiredService<ISiteContext>();
            var logger = sp.GetRequiredService<ILogger<AeroPageContentService>>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var cache = sp.GetService<IFusionCache>();
            var pageTreeService = sp.GetService<IPageTreeService>();
            var aliasWriter = sp.GetService<IPageRouteAliasWriter>();
            var contentValidator = sp.GetRequiredService<IHtmlContentValidator>();
            var styleCompiler = sp.GetRequiredService<IStyleCompiler>();
            var styleProfileResolver = sp.GetRequiredService<ISiteStyleProfileResolver>();
            var actor = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            return new AeroPageContentService(
                session,
                bus,
                siteContext,
                logger,
                contentValidator,
                styleCompiler,
                styleProfileResolver,
                actor,
                cache,
                pageTreeService,
                aliasWriter);
        });
        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroPageActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroPageActor>(0, "aero"));

        // Page hierarchy services
        services.AddScoped<IPageTreeService, PageTreeService>();
        services.AddScoped<INavigationService, NavigationService>();

        // Publishing workflow over the tracked PageDocument aggregate.
        services.AddScoped<IPagePublishingWorkflowService, PagePublishingWorkflowService>();

        // HTML page model, validation, and native style compilation.
        services.AddSingleton(_ => HtmlElementCatalog.CreateDefault());
        services.AddSingleton<IHtmlContentModelPolicy, HtmlContentModelPolicy>();
        services.AddSingleton<IHtmlAttributePolicy, HtmlAttributePolicy>();
        services.AddSingleton<IHtmlContentValidator>(sp => new HtmlContentValidator(
            sp.GetRequiredService<HtmlElementCatalog>(),
            sp.GetRequiredService<IHtmlContentModelPolicy>(),
            sp.GetRequiredService<IHtmlAttributePolicy>()));
        services.AddSingleton<IStyleCompiler, NativeCssStyleCompiler>();
        services.AddSingleton<HtmlStaticRenderer>();

        // FluentValidation
        services.AddScoped<IValidator<PageDocument>, PageDocumentValidator>();

        // HTTP context for audit/user tracking
        services.AddHttpContextAccessor();

        // Register this assembly so the Razor Pages in Areas/Cms/Pages are discovered
        services.AddRazorPages()
            .AddApplicationPart(typeof(PagesModule).Assembly);

        // Map area page routes ― without this, pages in Areas/Cms/Pages/ are only
        // reachable via the area-prefixed default (e.g. /Cms/Page). These conventions
        // expose them at the desired public URLs.
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Cms", "/page", "/");
            options.Conventions.AddAreaPageRoute("Cms", "/page", "/{**slug}");
            options.Conventions.AddAreaPageRoute("Cms", "/page", "/_cms/preview/pages/drafts/{draftId:long}");
            options.Conventions.AddAreaPageRouteModelConvention("Cms", "/page", model =>
            {
                foreach (var selector in model.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (string.Equals(
                            template?.TrimStart('/'),
                            "_cms/preview/pages/drafts/{draftId:long}",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selector.EndpointMetadata.Add(new AuthorizeAttribute("site:read"));
                    }
                }
            });
        });
    }

    /// <summary>
    /// Configures page and slug-reservation document schemas.
    /// </summary>
    /// <param name="opts">The mutable Sable store options.</param>
    /// <remarks>
    /// Page documents use optimistic concurrency and soft deletion. Slug reservations
    /// are unique per site, culture, and normalized slug.
    /// </remarks>
public void Configure(StoreOptions opts)
    {
        // ── PageDocument ──────────────────────────────────────────────────
        opts.Schema.For<PageDocument>().Identity(x => x.Id);
        opts.Schema.For<PageDocument>().SetSchemaMode(SchemaMode.Flexible);
        opts.Schema.For<PageDocument>().UseOptimisticConcurrency = true;

        // Scalar indexes
        opts.Schema.For<PageDocument>().Index(x => x.SiteId);
        opts.Schema.For<PageDocument>().Index(x => x.Depth);
        opts.Schema.For<PageDocument>().Index(x => x.Order);
        opts.Schema.For<PageDocument>().Index(x => x.ParentId);
        opts.Schema.For<PageDocument>().Index(x => x.Path);
        opts.Schema.For<PageDocument>().Index(x => x.PublicationState);
        opts.Schema.For<PageDocument>().Index(x => x.IsHidden);
        opts.Schema.For<PageDocument>().Index(x => x.ShowInNavMenu);
        opts.Schema.For<PageDocument>().Index(x => x.Culture);
        opts.Schema.For<PageDocument>().Index(x => x.TranslationGroupId);

        // Compound indexes for common query patterns
        opts.Schema.For<PageDocument>().Index(x => new { x.SiteId, x.Path });
        opts.Schema.For<PageDocument>().Index(x => new { x.SiteId, x.PublicationState });
        opts.Schema.For<PageDocument>().Index(x => new { x.ParentId, x.PublicationState });

        // Unique index: no two pages share (SiteId, Culture, ParentId, Slug)
        opts.Schema.For<PageDocument>()
            .UniqueIndex(x => new { x.SiteId, x.Culture, x.ParentId, x.Slug });

        // Ngram index for efficient Path prefix matching (StartsWith queries)
        // NgramIndex not available in AeroDB

        // Soft-delete — auto-configured via ISoftDeleted on PageDocument
        opts.Schema.For<PageDocument>().SoftDeleted();

        // DuplicateField for DateTimeOffset (computed indexes don't support this type)
        opts.Schema.For<PageDocument>().Duplicate(x => x.PublishedOn);

        // ── ContentSlugDocument ───────────────────────────────────────────
        // DocumentAlias not available in AeroDB
        opts.Schema.For<ContentSlugDocument>().Index(x => x.SiteId);
        opts.Schema.For<ContentSlugDocument>().Index(x => x.Culture);
        opts.Schema.For<ContentSlugDocument>().UniqueIndex(x => new { x.SiteId, x.Culture, x.NormalizedSlug });

    }

    /// <inheritdoc />
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <inheritdoc />
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapPagesApi();
        builder.MapPagesTreeApi();
        return Task.CompletedTask;
    }
}



