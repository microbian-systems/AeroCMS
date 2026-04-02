using Aero.Cms.Core;
using Aero.Cms.Services;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Blog;

public sealed class BlogModule : AeroModuleBase, IUiModule
{
    public override string Name => nameof(BlogModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [nameof(Pages.PagesModule)];
    public override IReadOnlyList<string> Category => ["content", "blog"];
    public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IBlogPostContentService, MartenBlogPostContentService>();
        services.AddHttpClient<IStaticPhotosClient, StaticPhotosClient>(client =>
        {
            client.BaseAddress = new Uri("https://static.photos/");
        });
        services.AddHttpClient<IPicsumPhotosClient, PicsumPhotosClient>(client =>
        {
            client.BaseAddress = new Uri("https://picsum.photos/");
        });

        // Register this assembly so the Razor Pages in Areas/Blog/Pages are discovered
        services.AddRazorPages()
            .AddApplicationPart(typeof(BlogModule).Assembly);

        // Map area page routes — without this, pages in Areas/Blog/Pages/ are only
        // reachable via the area-prefixed default (e.g. /Blog/BlogIndexPage).
        // These conventions expose them at the desired public URLs.
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Blog", "/BlogIndexPage", "/blog");
            options.Conventions.AddAreaPageRoute("Blog", "/BlogDetailPage", "/blog/{slug}");

            // Map Admin area routes — these must be explicitly mapped because
            // PagesModule's catch-all /{slug?} route would otherwise intercept them.
            options.Conventions.AddAreaPageRoute("Admin", "/Index", "/admin/blog");
            options.Conventions.AddAreaPageRoute("Admin", "/Edit", "/admin/blog/edit/{id?}");
        });
    }
}
