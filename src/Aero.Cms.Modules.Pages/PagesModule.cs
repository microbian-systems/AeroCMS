using Aero.Cms.Abstractions.Blocks.Editing;
using Aero.Cms.Core;

using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Core.Entities;
using Aero.Modular;

namespace Aero.Cms.Modules.Pages;

[Module(nameof(PagesModule))]
public sealed class PagesModule : AeroModuleBase, IConfigureMarten
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

        // Register this assembly so the Razor Pages in Areas/Cms/Pages are discovered
        services.AddRazorPages()
            .AddApplicationPart(typeof(PagesModule).Assembly);

        // Map area page routes — without this, pages in Areas/Cms/Pages/ are only
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
        opts.Schema.For<PageDocument>().DocumentAlias(Schemas.Tables.Pages);
        opts.Schema.For<PageDocument>().Identity(x => x.Id);
        opts.Schema.For<PageDocument>().Index(x => x.SiteId);
        opts.Schema.For<PageDocument>().UniqueIndex(x => x.SiteId, x => x.Slug);
        Configure<PageDocument>(services, opts);
        
        // ContentSlugDocument — composite unique on (SiteId, NormalizedSlug)
        opts.Schema.For<ContentSlugDocument>().DocumentAlias(Schemas.Tables.SlugRegistry);
        opts.Schema.For<ContentSlugDocument>().Index(x => x.SiteId);
        opts.Schema.For<ContentSlugDocument>().UniqueIndex(x => x.SiteId, x => x.NormalizedSlug);
        Configure<ContentSlugDocument>(services, opts);

        // PageDraft — one per page, upserted on auto-save, deleted on publish/manual save
        opts.Schema.For<PageDraft>().Index(x => x.PageId);
        opts.Schema.For<PageDraft>().Index(x => x.SiteId);
        opts.Schema.For<PageDraft>().Index(x => x.DraftedAt);
        Configure<PageDraft>(services, opts);
    }
}
