using Aero.Cms.Abstractions.Blocks.Editing;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentValidation;
using Hydro.Configuration;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddScoped<IPageContentService, MartenPageContentService>();
        services.AddSingleton<BlockEditingService>();

        // Page hierarchy services
        services.AddScoped<IPageTreeService, PageTreeService>();
        services.AddScoped<INavigationService, NavigationService>();

        // Publishing workflow (event sourcing)
        services.AddScoped<IPagePublishingWorkflowService, PagePublishingWorkflowService>();

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
            options.Conventions.AddAreaPageRoute("Cms", "/Page", "/{slug?}");
            options.Conventions.AddAreaPageRoute("Cms", "/Page", "/_cms/preview/pages/drafts/{draftId:long}");
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // ── Event Store ──────────────────────────────────────────────────
        // StreamIdentity is set globally in AeroAppServerExtensions.cs.
        // NOTE: Snapshot<T>() does not support long identity types (Snowflake).
        // Events are stored for audit/version history; documents use dual-write
        // (session.Store + session.Events.Append). See ADR #19.

        // ── PageDocument ──────────────────────────────────────────────────
        opts.Schema.For<PageDocument>().DocumentAlias(Schemas.Tables.Pages);
        opts.Schema.For<PageDocument>().Identity(x => x.Id);
        opts.Schema.For<PageDocument>().Index(x => x.SiteId);

        // Unique index: no two pages share (SiteId, ParentId, Slug)
        // Marten defaults to computed index type
        opts.Schema.For<PageDocument>()
            .UniqueIndex(x => x.SiteId, x => x.ParentId, x => x.Slug);

        // Hierarchy indexes (default to computed indexes in Marten)
        opts.Schema.For<PageDocument>()
            .Index(x => x.Path);
        opts.Schema.For<PageDocument>()
            .Index(x => x.ParentId);
        opts.Schema.For<PageDocument>()
            .NgramIndex(x => x.Path);

        // Soft-delete — auto-configured via ISoftDeleted on PageDocument
        opts.Schema.For<PageDocument>().SoftDeleted();

        // DuplicateField for DateTimeOffset (computed indexes don't support this type)
        opts.Schema.For<PageDocument>().Duplicate(x => x.PublishedOn);

        Configure<PageDocument>(services, opts);

        // ── ContentSlugDocument ───────────────────────────────────────────
        opts.Schema.For<ContentSlugDocument>().DocumentAlias(Schemas.Tables.SlugRegistry);
        opts.Schema.For<ContentSlugDocument>().Index(x => x.SiteId);
        opts.Schema.For<ContentSlugDocument>().UniqueIndex(x => x.SiteId, x => x.NormalizedSlug);
        Configure<ContentSlugDocument>(services, opts);

        // ── PageDraft ─────────────────────────────────────────────────────
        opts.Schema.For<PageDraft>().Index(x => x.PageId);
        opts.Schema.For<PageDraft>().Index(x => x.SiteId);
        opts.Schema.For<PageDraft>().Index(x => x.DraftedAt);
        Configure<PageDraft>(services, opts);
    }
}
