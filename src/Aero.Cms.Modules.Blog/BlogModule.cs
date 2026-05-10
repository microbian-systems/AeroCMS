using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Blog.Areas.Api.v1;
using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Modules.Blog.Parsers;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Services.Images;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Blog;

[Module(nameof(BlogModule))]
public sealed class BlogModule : AeroWebModule, IUiModule
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

        // Blog import services
        services.AddScoped<IBlogImportParser, JsonBlogImportParser>();
        services.AddScoped<IBlogImportParser, MarkdownBlogImportParser>();
        services.AddScoped<IBlogImportParser, ZipBlogImportParser>();
        services.AddScoped<IBlogImportService, BlogImportService>();

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
            options.Conventions.AddAreaPageRoute("Blog", "/BlogDetailPage", "/_cms/preview/blog/drafts/{draftId:long}");

            // Map Admin area routes — these must be explicitly mapped because
            // PagesModule's catch-all /{slug?} route would otherwise intercept them.
            options.Conventions.AddAreaPageRoute("Admin", "/Index", "/admin/blog");
            options.Conventions.AddAreaPageRoute("Admin", "/Edit", "/admin/blog/edit/{id?}");
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<BlogPostDocument>().DocumentAlias(Schemas.Tables.Posts);
        opts.Schema.For<BlogPostDocument>().Identity(x => x.Id);
        opts.Schema.For<BlogPostDocument>().Index(x => x.SiteId);
        opts.Schema.For<BlogPostDocument>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<BlogPostDocument>().Index(x => x.PublishedOn);
        opts.Schema.For<BlogPostDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<BlogPostDocument>().Index(x => x.ModifiedOn);
        
        // Tags and Categories
        opts.Schema.For<Tag>().Index(x => x.SiteId);
        opts.Schema.For<Tag>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<Category>().Index(x => x.SiteId);
        opts.Schema.For<Category>().UniqueIndex(x => x.SiteId, x => x.Slug);
    }
}
