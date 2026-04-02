using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Core;
using Marten;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsModule : AeroModuleBase
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
        //opts.Schema.For<DocsPage>().Duplicate(x => x.Title);
        opts.Schema.For<DocsPage>().Index(x => x.Slug);
        opts.Schema.For<DocsPage>().Index(x => x.ParentId);
        opts.Schema.For<DocsPage>().Index(x => x.Order);
        opts.Schema.For<DocsPage>().Index(x => x.PublishedOn);
        opts.Schema.For<DocsPage>().Index(x => x.CreatedOn);
        opts.Schema.For<DocsPage>().Index(x => x.ModifiedOn);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IDocsService, DocsService>();
    }

}
