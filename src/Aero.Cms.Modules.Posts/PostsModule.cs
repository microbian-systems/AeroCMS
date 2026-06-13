using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Validators;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Modules.Posts.Areas.Api.v1;
using Aero.Cms.Modules.Posts.Parsers;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Services.Images;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActorCreateSeriesRequest = Aero.Cms.Abstractions.Requests.CreateSeriesRequest;
using ActorUpdateSeriesRequest = Aero.Cms.Abstractions.Requests.UpdateSeriesRequest;

namespace Aero.Cms.Modules.Posts;

[Module(nameof(PostsModule))]
public sealed class PostsModule : AeroWebModule, IUiModule
{
    public override string Name => nameof(PostsModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [nameof(Pages.PagesModule)];
    public override IReadOnlyList<string> Category => ["content", "blog"];
    public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IPostContentService, PostContentService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddHttpClient<IStaticPhotosClient, StaticPhotosClient>(client =>
        {
            client.BaseAddress = new Uri("https://static.photos/");
        });
        services.AddHttpClient<IPicsumPhotosClient, PicsumPhotosClient>(client =>
        {
            client.BaseAddress = new Uri("https://picsum.photos/");
        });

        // Blog import services
        services.AddScoped<IPostImportParser, JsonPostImportParser>();
        services.AddScoped<IPostImportParser, MarkdownPostImportParser>();
        services.AddScoped<IPostImportParser, ZipPostImportParser>();
        services.AddScoped<IPostImportService, PostsImportService>();

        // Grain-backed actors — direct injection for thin API controllers
        services.AddSingleton<IAeroPostActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroPostActor>(0, "aero"));
        services.AddSingleton<IAeroTagActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroTagActor>(0, "aero"));
        services.AddSingleton<IAeroCategoryActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroCategoryActor>(0, "aero"));
        services.AddSingleton<IAeroSeriesActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroSeriesActor>(0, "aero"));

        // FluentValidation validators
        services.AddScoped<IValidator<CreateTagRequest>, TagRequestValidator>();
        services.AddScoped<IValidator<UpdateTagRequest>, UpdateTagRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
        services.AddScoped<IValidator<DeleteCategoryRequest>, DeleteCategoryRequestValidator>();
        services.AddScoped<IValidator<ActorCreateSeriesRequest>, CreateSeriesRequestValidator>();
        services.AddScoped<IValidator<ActorUpdateSeriesRequest>, UpdateSeriesRequestValidator>();

        // Register this assembly so the Razor Pages in Areas/Blog/Pages are discovered
        services.AddRazorPages()
            .AddApplicationPart(typeof(PostsModule).Assembly);

        // Map area page routes — without this, pages in Areas/Blog/Pages/ are only
        // reachable via the area-prefixed default (e.g. /Blog/PostsIndexPage).
        // These conventions expose them at the desired public URLs.
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Blog", "/PostsIndexPage", "/blog");
            options.Conventions.AddAreaPageRoute("Blog", "/PostsIndexPage", "/{culture}/blog");
            options.Conventions.AddAreaPageRoute("Blog", "/PostsDetailPage", "/blog/{slug}");
            options.Conventions.AddAreaPageRoute("Blog", "/PostsDetailPage", "/{culture}/blog/{slug}");
            options.Conventions.AddAreaPageRoute("Blog", "/PostsDetailPage", "/_cms/preview/blog/drafts/{draftId:long}");
        });
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<PostDocument>().DocumentAlias(Schemas.Tables.Posts);
        opts.Schema.For<PostDocument>().Identity(x => x.Id);
        opts.Schema.For<PostDocument>().Index(x => x.SiteId);
        opts.Schema.For<PostDocument>().Index(x => x.Culture);
        opts.Schema.For<PostDocument>().Index(x => x.TranslationGroupId);
        opts.Schema.For<PostDocument>().Index(x => x.SeriesId);
        opts.Schema.For<PostDocument>().UniqueIndex(x => x.SiteId, x => x.Culture, x => x.Slug);
        opts.Schema.For<PostDocument>().Index(x => x.PublishedOn);
        opts.Schema.For<PostDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<PostDocument>().Index(x => x.ModifiedOn);
        
        // Tags and Categories
        opts.Schema.For<Tag>().Index(x => x.SiteId);
        opts.Schema.For<Tag>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<Category>().Index(x => x.SiteId);
        opts.Schema.For<Category>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<Series>().Index(x => x.SiteId);
        opts.Schema.For<Series>().UniqueIndex(x => x.SiteId, x => x.Slug);
        opts.Schema.For<TagTranslation>().Index(x => x.TagId);
        opts.Schema.For<TagTranslation>().Index(x => x.Culture);
        opts.Schema.For<TagTranslation>().UniqueIndex(x => x.TagId, x => x.Culture);
        opts.Schema.For<CategoryTranslation>().Index(x => x.CategoryId);
        opts.Schema.For<CategoryTranslation>().Index(x => x.Culture);
        opts.Schema.For<CategoryTranslation>().UniqueIndex(x => x.CategoryId, x => x.Culture);
        opts.Schema.For<SeriesTranslation>().Index(x => x.SeriesId);
        opts.Schema.For<SeriesTranslation>().Index(x => x.Culture);
        opts.Schema.For<SeriesTranslation>().UniqueIndex(x => x.SeriesId, x => x.Culture);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapCategoriesApi();
        builder.MapTagsApi();
        builder.MapSeriesApi();
        builder.MapBlogApi();

        return Task.CompletedTask;
    }
}
