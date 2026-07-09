using Aero.Cms.Abstractions.Blocks.Editing;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Pages.Admin;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Modules.Pages.CustomComponents;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Core.Http;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectionLifecycle = JasperFx.Events.Projections.ProjectionLifecycle;

namespace Aero.Cms.Modules.Pages;

[Module(nameof(PagesModule))]
public sealed class PagesModule : AeroWebModule, IConfigureMarten
{
    public override string Name => nameof(PagesModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content", "pages"];
    public override IReadOnlyList<string> Tags => ["content", "pages", "cms"];


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
            var logger = sp.GetRequiredService<ILogger<MartenPageContentService>>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var cache = sp.GetService<IFusionCache>();
            var pageTreeService = sp.GetService<IPageTreeService>();
            var actor = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            return new MartenPageContentService(session, bus, siteContext, logger, actor, cache, pageTreeService);
        });
        services.AddSingleton<BlockEditingService>();

        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroPageActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroPageActor>(0, "aero"));

        // Page hierarchy services
        services.AddScoped<IPageTreeService, PageTreeService>();
        services.AddScoped<INavigationService, NavigationService>();

        // Publishing workflow (event sourcing)
        services.AddScoped<IPagePublishingWorkflowService, PagePublishingWorkflowService>();

        // Admin status (read-model comparing published vs draft versions)
        services.AddSingleton<PageAdminStatusService>();

        // Page delete cleanup (hard-deletes PageEditorState)
        services.AddScoped<PageDeleteHandler>();

        // Shared layout manifest builder (used by both preview and publish)
        services.AddSingleton<IPageLayoutManifestBuilder, PageLayoutManifestBuilder>();

        // Preview pipeline (transient layout from draft state)
        services.AddScoped<IPagePreviewService, PagePreviewService>();

        // Neo editor catalog
        services.AddSingleton<INeoEditorCatalogProvider, NeoEditorCatalogProvider>();

        // FluentValidation
        services.AddScoped<IValidator<PageDocument>, PageDocumentValidator>();
        services.AddScoped<
            IValidator<SavePageCustomComponentRequest>,
            SavePageCustomComponentRequestValidator>();
        services.AddScoped<IPageCustomComponentService, PageCustomComponentService>();

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
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // ── Event Store ──────────────────────────────────────────────────
        // Custom IProjection registers inline. Uses MartenReal alias because
        // global using Marten resolves to Aero.Marten (shim), not real Marten NuGet.
        opts.Projections.Add(new PageDocumentProjection(), ProjectionLifecycle.Inline);
        opts.Projections.Add(new PageCompositionProjection(), ProjectionLifecycle.Inline);

        // ── PageDocument ──────────────────────────────────────────────────
        opts.Schema.For<PageDocument>().DocumentAlias(Schemas.Tables.Pages);
        opts.Schema.For<PageDocument>().Identity(x => x.Id);
        opts.Schema.For<PageDocument>().UseOptimisticConcurrency(true);

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
            .UniqueIndex(x => x.SiteId, x => x.Culture, x => x.ParentId, x => x.Slug);

        // Ngram index for efficient Path prefix matching (StartsWith queries)
        opts.Schema.For<PageDocument>().NgramIndex(x => x.Path);

        // Soft-delete — auto-configured via ISoftDeleted on PageDocument
        opts.Schema.For<PageDocument>().SoftDeleted();

        // DuplicateField for DateTimeOffset (computed indexes don't support this type)
        opts.Schema.For<PageDocument>().Duplicate(x => x.PublishedOn);

        Configure<PageDocument>(services, opts);

        // ── PageCompositionDocument ──────────────────────────────────────
        opts.Schema.For<PageCompositionDocument>().Identity(x => x.Id);
        opts.Schema.For<PageCompositionDocument>().Index(x => x.SiteId);
        opts.Schema.For<PageCompositionDocument>().Index(x => x.PageId);
        opts.Schema.For<PageCompositionDocument>().Index(x => x.Culture);
        opts.Schema.For<PageCompositionDocument>().Index(x => x.State);
        opts.Schema.For<PageCompositionDocument>().Index(x => new { x.PageId, x.State });
        opts.Schema.For<PageCompositionDocument>().Index(x => new { x.SiteId, x.Culture });
        Configure<PageCompositionDocument>(services, opts);

        // ── PageNodeIndexDocument ────────────────────────────────────────
        opts.Schema.For<PageNodeIndexDocument>().Identity(x => x.Id);
        opts.Schema.For<PageNodeIndexDocument>().Index(x => x.SiteId);
        opts.Schema.For<PageNodeIndexDocument>().Index(x => x.PageId);
        opts.Schema.For<PageNodeIndexDocument>().Index(x => x.CompositionId);
        opts.Schema.For<PageNodeIndexDocument>().Index(x => x.CatalogId);
        opts.Schema.For<PageNodeIndexDocument>().Index(x => new { x.SiteId, x.CatalogId });

        // ── ContentSlugDocument ───────────────────────────────────────────
        opts.Schema.For<ContentSlugDocument>().DocumentAlias(Schemas.Tables.SlugRegistry);
        opts.Schema.For<ContentSlugDocument>().Index(x => x.SiteId);
        opts.Schema.For<ContentSlugDocument>().Index(x => x.Culture);
        opts.Schema.For<ContentSlugDocument>().UniqueIndex(x => x.SiteId, x => x.Culture, x => x.NormalizedSlug);
        Configure<ContentSlugDocument>(services, opts);

        // ── PageDraft ─────────────────────────────────────────────────────
        opts.Schema.For<PageDraft>().Index(x => x.PageId);
        opts.Schema.For<PageDraft>().Index(x => x.SiteId);
        opts.Schema.For<PageDraft>().Index(x => x.DraftedAt);
        Configure<PageDraft>(services, opts);

        // ── PageCustomComponent ────────────────────────────────────────────
        opts.Schema.For<PageCustomComponent>().Index(x => x.SiteId);
        opts.Schema.For<PageCustomComponent>().Index(x => x.Name);
        opts.Schema.For<PageCustomComponent>().Index(x => x.UpdatedAt);
        opts.Schema.For<PageCustomComponent>()
            .UniqueIndex(x => x.SiteId, x => x.Name);
        Configure<PageCustomComponent>(services, opts);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        using var scope = builder.ServiceProvider.CreateScope();
        builder.MapPagesApi();
        builder.MapPagesTreeApi();
        builder.MapPageCustomComponentsApi();
        return Task.CompletedTask;
    }
}
