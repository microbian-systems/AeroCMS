using Aero.Cms.Modules.Docs.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Core.Http;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Docs;

[Module(nameof(DocsModule))]
public sealed class DocsModule : AeroWebModule
{
    public override string Name => nameof(DocsModule);
    public override string Version =>AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override short Order => 100;

    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["documentation", "knowledge base"];
    public override IReadOnlyList<string> Tags => ["docs", "markdown", "kbase"];


    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<DocsPage>().DocumentAlias(Schemas.Tables.Docs);
        opts.Schema.For<DocsPage>().UseOptimisticConcurrency(true);
        opts.Schema.For<DocsPage>().Index(x => x.SiteId);
        opts.Schema.For<DocsPage>().UniqueIndex(x => x.SiteId, x => x.Culture, x => x.Slug);
        opts.Schema.For<DocsPage>().Index(x => x.Culture);
        opts.Schema.For<DocsPage>().Index(x => x.TranslationGroupId);
        opts.Schema.For<DocsPage>().Index(x => x.ParentId);
        opts.Schema.For<DocsPage>().Index(x => x.Order);
        opts.Schema.For<DocsPage>().Index(x => x.PublishedOn);
        opts.Schema.For<DocsPage>().Index(x => x.CreatedOn);
        opts.Schema.For<DocsPage>().Index(x => x.ModifiedOn);

        // Full-text search (Phase 1)
        opts.Schema.For<DocsPage>().NgramIndex(x => x.Title);
        opts.Schema.For<DocsPage>().NgramIndex(x => x.MarkdownContent);

        // Editor state (Phase 1)
        opts.Schema.For<DocsEditorState>().Identity(x => x.Id);
        opts.Schema.For<DocsEditorState>().Index(x => x.SiteId);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Docs", "/DocsIndex", "/{culture}/docs");
            options.Conventions.AddAreaPageRoute("Docs", "/Doc", "/{culture}/docs/{*slug}");
        });

        // Content service — factory resolves ISiteContext + IHttpContextAccessor
        // at the boundary and converts them to explicit primitives so the service
        // never touches HTTP transport concerns.
        services.AddScoped<IDocsService>(sp =>
        {
            var session = sp.GetRequiredService<IDocumentSession>();
            var blockService = sp.GetRequiredService<IBlockService>();
            var bus = sp.GetRequiredService<IMessageBus>();
            var siteContext = sp.GetRequiredService<ISiteContext>();
            var logger = sp.GetRequiredService<ILogger<DocsContentService>>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var cache = sp.GetService<IFusionCache>();
            var actor = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            return new DocsContentService(session, blockService, bus, siteContext, logger, actor, cache);
        });
        services.AddScoped<IDocsTreeService, DocsTreeService>();

        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroDocsActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroDocsActor>(0, "aero"));
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapDocsApi();
        return Task.CompletedTask;
    }
}
