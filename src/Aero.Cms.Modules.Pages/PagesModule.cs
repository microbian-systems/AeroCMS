using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;
using Aero.Core.Http;
using Aero.Core.Security;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using FluentValidation;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Rendering;
using Aero.Cms.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            var contentReferenceValidator = sp.GetService<IContentCompositionReferenceValidator>();
            var registeredFragmentRegistry = sp.GetService<IPageRegisteredFragmentRegistry>();
            var pageRendererRegistry = sp.GetRequiredService<IPageRendererRegistry>();
            var pageSourceVersionStore = sp.GetRequiredService<IPageSourceVersionStore>();
            var pageContentQueryResolver = sp.GetRequiredService<IPageContentQueryResolver>();
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
                aliasWriter,
                contentReferenceValidator,
                registeredFragmentRegistry,
                pageRendererRegistry,
                pageSourceVersionStore,
                pageContentQueryResolver);
        });
        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroPageActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroPageActor>(0, "aero"));

        // Page hierarchy services
        services.AddScoped<IPageTreeService, PageTreeService>();
        services.AddScoped<INavigationService, NavigationService>();

        // Publishing workflow over the tracked PageDocument aggregate.
        services.AddScoped<IPagePublishingWorkflowService, PagePublishingWorkflowService>();
        services.AddScoped<IPageSourceVersionStore, PageSourceVersionStore>();
        services.AddScoped<
            IContentReferenceSourceProvider,
            PageContentReferenceSourceProvider>();

        // HTML page model, validation, and native style compilation.
        services.AddSingleton(_ => HtmlElementCatalog.CreateDefault());
        services.AddSingleton<IHtmlContentModelPolicy, HtmlContentModelPolicy>();
        services.AddSingleton<IHtmlAttributePolicy, HtmlAttributePolicy>();
        services.AddSingleton<IHtmlContentValidator>(sp => new HtmlContentValidator(
            sp.GetRequiredService<HtmlElementCatalog>(),
            sp.GetRequiredService<IHtmlContentModelPolicy>(),
            sp.GetRequiredService<IHtmlAttributePolicy>()));
        services.AddSingleton<IHtmlFragmentImporter, HtmlFragmentImporter>();
        services.AddSingleton<IMarkdownInterchangeAdapter, MarkdownInterchangeAdapter>();
        services.AddSingleton<IStyleCompiler, NativeCssStyleCompiler>();
        services.AddSingleton<HtmlStaticRenderer>();
        services.AddSingleton<IPageFragmentRenderer, MarkdownPageFragmentRenderer>();
        services.AddSingleton<IPageFragmentRenderer, CustomHtmlPageFragmentRenderer>();
        services.TryAddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        services.TryAddSingleton<SecureScribanTemplateOptions>();
        services.TryAddSingleton<ISecureScribanRenderer, SecureScribanRenderer>();
        services.AddSingleton<IPageFragmentRenderer, ScribanPageFragmentRenderer>();
        services.AddSingleton<ISharpTsExecutor, SharpTsExecutor>();
        services.AddSingleton<IPageFragmentRenderer, SharpTsPageFragmentRenderer>();
        services.AddSingleton<IPageFragmentRenderer, HtmxPageFragmentRenderer>();
        services.AddPageRegisteredFragment<SiteNoticePageRegisteredFragmentProvider>();
        services.AddSingleton<IPageRegisteredFragmentRegistry, PageRegisteredFragmentRegistry>();
        services.TryAddScoped<IContentCompositionResolver, UnavailableContentCompositionResolver>();
        services.TryAddScoped<IContentHierarchyQueryService, UnavailableContentHierarchyQueryService>();
        services.AddScoped<IPageContentQueryResolver, PageContentQueryResolver>();
        services.AddScoped<PageCompositionExpander>();
        services.AddScoped<PageMarkupRenderer>();
        services.AddScoped<IPageRenderer, AeroCompositionPageRenderer>();
        services.AddScoped<IPageRenderer, ScribanPageRenderer>();
        services.AddScoped<IPageRenderer, SharpTsPageRenderer>();
        services.AddScoped<IPageRenderer, HtmxPageRenderer>();
        services.AddScoped<IPageRendererRegistry, PageRendererRegistry>();

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
        var pages = opts.Schema.For<PageDocument>()
            .TableName(Schemas.Tables.Pages);
        pages.Identity(x => x.Id);
        pages.SetSchemaMode(SchemaMode.Flexible);
        pages.UseOptimisticConcurrency = true;

        // Scalar indexes
        pages.Index(x => x.SiteId);
        pages.Index(x => x.Depth);
        pages.Index(x => x.Order);
        pages.Index(x => x.ParentId);
        pages.Index(x => x.Path);
        pages.Index(x => x.PublicationState);
        pages.Index(x => x.IsHidden);
        pages.Index(x => x.ShowInNavMenu);
        pages.Index(x => x.Culture);
        pages.Index(x => x.TranslationGroupId);

        // Compound indexes for common query patterns
        pages.Index(x => new { x.SiteId, x.Path });
        pages.Index(x => new { x.SiteId, x.PublicationState });
        pages.Index(x => new { x.ParentId, x.PublicationState });

        // Unique index: no two pages share (SiteId, Culture, ParentId, Slug)
        pages.UniqueIndex(x => new { x.SiteId, x.Culture, x.ParentId, x.Slug });

        // Ngram index for efficient Path prefix matching (StartsWith queries)
        // NgramIndex not available in AeroDB

        // Soft-delete — auto-configured via ISoftDeleted on PageDocument
        pages.SoftDeleted();

        // DuplicateField for DateTimeOffset (computed indexes don't support this type)
        pages.Duplicate(x => x.PublishedOn);

        // ── PageSourceVersion ─────────────────────────────────────────────
        var pageSourceVersions = opts.Schema.For<PageSourceVersion>()
            .TableName(Schemas.Tables.PageSourceVersions);
        pageSourceVersions.Identity(x => x.Id);
        pageSourceVersions.SetSchemaMode(SchemaMode.Flexible);
        pageSourceVersions.Index(x => x.SiteId);
        pageSourceVersions.Index(x => x.PageId);
        pageSourceVersions.Index(x => x.RendererId);
        pageSourceVersions.Index(x => new { x.SiteId, x.PageId, x.RendererId });

        // ── ContentSlugDocument ───────────────────────────────────────────
        var contentSlugs = opts.Schema.For<ContentSlugDocument>()
            .TableName(Schemas.Tables.ContentSlugs);
        contentSlugs.Index(x => x.SiteId);
        contentSlugs.Index(x => x.Culture);
        contentSlugs.UniqueIndex(x => new { x.SiteId, x.Culture, x.NormalizedSlug });

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



