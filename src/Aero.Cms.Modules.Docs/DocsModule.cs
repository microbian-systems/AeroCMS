using Aero.Cms.Modules.Docs.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Core;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;

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
        opts.Schema.For<DocsPage>().DocumentAlias("docs");
        opts.Schema.For<DocsPage>().Index(x => x.SiteId);
        opts.Schema.For<DocsPage>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<DocsPage>().Index(x => x.ParentId);
        opts.Schema.For<DocsPage>().Index(x => x.Order);
        opts.Schema.For<DocsPage>().Index(x => x.PublishedOn);
        opts.Schema.For<DocsPage>().Index(x => x.CreatedOn);
        opts.Schema.For<DocsPage>().Index(x => x.ModifiedOn);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IDocsService, DocsService>();

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
