using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Modules.Cache;
using Aero.Cms.Modules.Content.Caching;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Cms.Modules.Content.Events;
using Aero.Cms.Modules.Content.Rendering;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Content;

/// <summary>
/// Represents a class for ContentModule.
/// </summary>
[Module(nameof(ContentModule))]
public sealed class ContentModule : AeroWebModule, IContentDefinitionModule, IConfigureAeroDB
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(ContentModule);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public override string Description => "Runtime-defined content types with Scriban-based rendering. " +
        "Managers define content type schemas (fields, validation, templates) at runtime. " +
        "Content items are stored as field bags (Dictionary<string, JsonElement>) and rendered " +
        "directly through the secure Scriban pipeline.";

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [nameof(CacheModule)];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["content", "infrastructure"];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["content", "content-types", "cms", "structured-data"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Register the entire content type system via the extension method
        services.AddContentTypeSystem();
        services.AddScoped<ContentCacheInvalidator>();
        services.AddScoped<ContentEventPublisher>();
        services.Replace(ServiceDescriptor.Scoped<IContentTypeService, CachedContentTypeService>());
        services.Replace(ServiceDescriptor.Scoped<IContentService, CachedContentService>());

        // Public URL rendering for content types
        services.AddScoped<ContentTypeUrlRenderer>();
        services.AddRazorPages()
            .AddApplicationPart(typeof(ContentModule).Assembly);
        services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AddAreaPageRoute(
                "Content",
                "/PublicContent",
                "/content/{typeAlias}/{entrySlug}"));

        // Grain-backed actors — direct injection for thin API controllers
        services.AddSingleton<IAeroContentItemActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentItemActor>(0, "aero"));
        services.AddSingleton<IAeroContentTypeActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentTypeActor>(0, "aero"));
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
        // Register the content types and field editors through the builder
        builder.AddFieldEditor<TextFieldEditor>();
        builder.AddFieldEditor<ImageFieldEditor>();
        builder.AddFieldEditor<RichtextFieldEditor>();
        builder.AddFieldEditor<NumberFieldEditor>();
        builder.AddFieldEditor<BooleanFieldEditor>();
        builder.AddFieldEditor<UrlFieldEditor>();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions opts)
    {
        // AeroDB document configuration for the content type system
        opts.Schema.For<ContentTypeDocument>()
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.SiteId, x.Alias });

        opts.Schema.For<ContentItem>()
            .Index(x => x.SiteId)
            .Index(x => x.Slug)
            .Index(x => x.ContentTypeAlias);

        opts.Schema.For<ContentItemVersion>()
            .Index(x => x.ContentItemId);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapContentTypesApi();
        builder.MapContentItemsApi();

        return Task.CompletedTask;
    }
}
