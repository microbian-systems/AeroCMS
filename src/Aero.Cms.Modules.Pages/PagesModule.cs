using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Editing;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Pages;

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
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<PageDocument>().DocumentAlias(Schemas.Tables.Pages);
        //opts.Schema.For<PageDocument>().Duplicate(x => x.Title); // todo - find out what the marten For<T>().Duplicate() method does and if it is needed here
        opts.Schema.For<PageDocument>().Index(x => x.Slug);
        opts.Schema.For<PageDocument>().Index(x => x.PublishedOn);
        opts.Schema.For<PageDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<PageDocument>().Index(x => x.ModifiedOn);
    }
}
