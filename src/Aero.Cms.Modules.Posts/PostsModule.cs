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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ActorCreateSeriesRequest = Aero.Cms.Abstractions.Requests.CreateSeriesRequest;
using ActorUpdateSeriesRequest = Aero.Cms.Abstractions.Requests.UpdateSeriesRequest;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Integrates post persistence, actors, import services, public Razor Pages, and admin endpoints.
/// </summary>
[Module(nameof(PostsModule))]
public sealed class PostsModule : AeroWebModule, IUiModule, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(PostsModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [nameof(Pages.PagesModule)];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["content", "blog"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["content", "blog", "cms"];

    /// <summary>
    /// Registers post services, import strategies, actor proxies, validators, and public blog routes.
    /// </summary>
    /// <param name="services">The application service collection to extend.</param>
    /// <param name="config">The optional host configuration; this implementation does not read it.</param>
    /// <param name="env">The optional host environment; this implementation does not read it.</param>
    /// <remarks>
    /// Actor interfaces resolve singleton Orleans proxies for the shared <c>0/aero</c> grain identity.
    /// The method also adds this assembly as a Razor Pages application part and exposes both
    /// culture-prefixed and unprefixed blog routes.
    /// </remarks>
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
            options.Conventions.AddAreaPageRouteModelConvention("Blog", "/PostsDetailPage", model =>
            {
                foreach (var selector in model.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (string.Equals(
                            template?.TrimStart('/'),
                            "_cms/preview/blog/drafts/{draftId:long}",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selector.EndpointMetadata.Add(new AuthorizeAttribute("site:read"));
                    }
                }
            });
        });
    }

    /// <summary>
    /// Defines Sable identities, lookup indexes, and per-site uniqueness constraints for post data.
    /// </summary>
    /// <param name="opts">The mutable Sable store options.</param>
    /// <remarks>
    /// Post slugs are unique per site and culture. Tag, category, and series slugs are unique per
    /// site, while taxonomy translations are unique per owner and culture.
    /// </remarks>
public void Configure(StoreOptions opts)
    {
        opts.Schema.For<PostDocument>().Identity(x => x.Id);
        opts.Schema.For<PostDocument>().Index(x => x.SiteId);
        opts.Schema.For<PostDocument>().Index(x => x.Culture);
        opts.Schema.For<PostDocument>().Index(x => x.TranslationGroupId);
        opts.Schema.For<PostDocument>().Index(x => x.SeriesId);
        opts.Schema.For<PostDocument>().UniqueIndex(x => new { x.SiteId, x.Culture, x.Slug });
        opts.Schema.For<PostDocument>().Index(x => x.PublishedOn);
        opts.Schema.For<PostDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<PostDocument>().Index(x => x.ModifiedOn);
        
        // Tags and Categories
        opts.Schema.For<Tag>().Index(x => x.SiteId);
        opts.Schema.For<Tag>().UniqueIndex(x => new { x.SiteId, x.Slug });
        opts.Schema.For<Category>().Index(x => x.SiteId);
        opts.Schema.For<Category>().UniqueIndex(x => new { x.SiteId, x.Slug });
        opts.Schema.For<Series>().Index(x => x.SiteId);
        opts.Schema.For<Series>().UniqueIndex(x => new { x.SiteId, x.Slug });
        opts.Schema.For<TagTranslation>().Index(x => x.TagId);
        opts.Schema.For<TagTranslation>().Index(x => x.Culture);
        opts.Schema.For<TagTranslation>().UniqueIndex(x => new { x.TagId, x.Culture });
        opts.Schema.For<CategoryTranslation>().Index(x => x.CategoryId);
        opts.Schema.For<CategoryTranslation>().Index(x => x.Culture);
        opts.Schema.For<CategoryTranslation>().UniqueIndex(x => new { x.CategoryId, x.Culture });
        opts.Schema.For<SeriesTranslation>().Index(x => x.SeriesId);
        opts.Schema.For<SeriesTranslation>().Index(x => x.Culture);
        opts.Schema.For<SeriesTranslation>().UniqueIndex(x => new { x.SeriesId, x.Culture });
    }

    /// <summary>
    /// Applies the same store configuration when invoked through the service-aware configuration hook.
    /// </summary>
    /// <param name="services">The service provider; this implementation does not resolve services.</param>
    /// <param name="opts">The mutable Sable store options.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Maps the category, tag, series, post, import, and preview HTTP endpoints.
    /// </summary>
    /// <param name="builder">The endpoint route builder to extend.</param>
    /// <returns>A completed task after all routes have been registered.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapCategoriesApi();
        builder.MapTagsApi();
        builder.MapSeriesApi();
        builder.MapBlogApi();

        return Task.CompletedTask;
    }
}
