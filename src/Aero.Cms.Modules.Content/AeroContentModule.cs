using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Composition;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Views;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Modules.Cache;
using Aero.Cms.Modules.Content.Caching;
using Aero.Cms.Modules.Content.Composition;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Cms.Modules.Content.Events;
using Aero.Cms.Modules.Content.Rendering;
using Aero.Cms.Modules.Content.Routing;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Content;

/// <summary>
/// Registers runtime-defined content schemas, items, rendering, caching, actors, and HTTP endpoints.
/// </summary>
[Module(nameof(ContentModule))]
public sealed class ContentModule : AeroWebModule, IContentDefinitionModule, IConfigureAeroDB
{
        /// <inheritdoc />
public override string Name => nameof(ContentModule);

        /// <inheritdoc />
public override string Version => AeroConstants.Version;

        /// <inheritdoc />
public override string Author => AeroConstants.Author;

        /// <inheritdoc />
public override string Description => "Runtime-defined content types with Scriban-based rendering. " +
        "Managers define content type schemas (fields, validation, templates) at runtime. " +
        "Content items are stored as field bags (Dictionary<string, JsonElement>) and rendered " +
        "directly through the secure Scriban pipeline.";

        /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [nameof(CacheModule)];

        /// <inheritdoc />
public override IReadOnlyList<string> Category => ["content", "infrastructure"];

        /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["content", "content-types", "cms", "structured-data"];

        /// <summary>
    /// Registers the content system, cache decorators, public renderer, Razor Page route, and grain proxies.
    /// </summary>
    /// <param name="services">The service collection to mutate.</param>
    /// <param name="config">Module configuration; not used directly.</param>
    /// <param name="env">The host environment; not used directly.</param>
    /// <remarks>
    /// Existing <see cref="IContentTypeService"/> and <see cref="IContentService"/> registrations are
    /// replaced by scoped cache decorators. Both actor contracts resolve fixed Orleans grain keys.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Register the entire content type system via the extension method
        services.AddContentTypeSystem();
        // Do not let a privileged store registration become an implicit DDL enable switch.  The
        // default capability is intentionally immutable until the exact transaction/claim proof
        // exists for the bundled Sable + SurrealDB runtime.
        services.TryAddSingleton<IContentRelationshipSchemaCapabilityProvider, DisabledContentRelationshipSchemaCapabilityProvider>();
        services.TryAddScoped<IPublicSiteRouteResolver, DisabledPublicSiteRouteResolver>();
        services.Replace(ServiceDescriptor.Singleton<IContentViewOutputCacheInvalidator, ContentViewOutputCacheInvalidator>());
        services.AddScoped<ContentCacheInvalidator>();
        services.AddScoped<ContentEventPublisher>();
        services.Replace(ServiceDescriptor.Scoped<IContentTypeService, CachedContentTypeService>());
        services.Replace(ServiceDescriptor.Scoped<IContentService, CachedContentService>());
        services.Replace(ServiceDescriptor.Scoped<IContentHierarchyQueryService, ContentHierarchyQueryService>());
        services.AddScoped<ContentHierarchyManagerService>();
        services.AddScoped<IContentCompositionReferenceValidator, ContentCompositionReferenceValidator>();
        services.AddScoped<IContentCompositionResolver, ContentCompositionResolver>();

        // Public URL rendering for content types
        services.AddScoped<ContentTypeUrlRenderer>();
        services.AddTransient<PublicContentRouteTransformer>();
        services.AddRazorPages()
            .AddApplicationPart(typeof(ContentModule).Assembly);
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute(
                "Content",
                "/PublicContent",
                "/content/{typeAlias}/{entrySlug}");
        });

        // Grain-backed actors — direct injection for thin API controllers
        services.AddSingleton<IAeroContentItemActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentItemActor>(0, "aero"));
        services.AddSingleton<IAeroContentTypeActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentTypeActor>(0, "aero"));
    }

        /// <summary>
    /// Registers the built-in field-editor implementations with the module composition builder.
    /// </summary>
    /// <param name="builder">The builder that records editors and their scoped registrations.</param>
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
    /// Configures content-type uniqueness and content-item/version query indexes.
    /// </summary>
    /// <param name="opts">The document-store options whose schema is mutated.</param>
    /// <remarks>
    /// Content-type aliases are unique per site. Item slugs are indexed but are not declared unique
    /// by this schema configuration.
    /// </remarks>
public void Configure(StoreOptions opts)
    {
        opts.Schema.Analyzers.DefineAnalyzer(
            ContentSearchConstants.AnalyzerName,
            tokenizers:
            [
                Search.Tokenizer.Blank,
                Search.Tokenizer.Class,
                Search.Tokenizer.Punct
            ],
            filters:
            [
                Search.Filter.Lowercase,
                Search.Filter.Ascii
            ]);

        // AeroDB document configuration for the content type system
        opts.Schema.For<ContentTypeDocument>()
            .TableName(Schemas.Tables.ContentTypes)
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.SiteId, x.Alias });

        opts.Schema.For<ContentItem>()
            .TableName(Schemas.Tables.ContentItems)
            .Index(x => x.SiteId)
            .Index(x => x.Slug)
            .Index(x => x.ContentTypeAlias)
            .Index(x => x.ParentId)
            .UniqueIndex(x => new { x.SiteId, x.ContentTypeAlias, x.TranslationGroupId, x.Culture })
            .UniqueIndex(x => new { x.SiteId, x.ContentTypeAlias, x.Culture, x.Slug });
        opts.Schema.For<ContentItem>().UseOptimisticConcurrency = true;

        opts.Schema.For<ContentTranslationGroupDocument>()
            .TableName("content_translation_groups")
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.SiteId, x.ContentTypeAlias, x.Id });
        opts.Schema.For<ContentTranslationGroupDocument>().UseOptimisticConcurrency = true;

        opts.Schema.For<ContentTranslationProjectionWorkDocument>()
            .TableName("content_translation_projection_work")
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.SiteId, x.TranslationGroupId, x.GroupStorageVersion });
        opts.Schema.For<ContentTranslationProjectionWorkDocument>().UseOptimisticConcurrency = true;

        opts.Schema.For<ContentItemVersion>()
            .TableName(Schemas.Tables.ContentItemVersions)
            .Index(x => x.ContentItemId);

        opts.Schema.For<ContentSurrealViewDocument>()
            .TableName("content_surreal_view_revisions")
            .Identity(x => x.Id)
            .Index(x => x.TenantId)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.TenantId, x.SiteId, x.Alias, x.IsPublished, x.Version });

        opts.Schema.For<ContentRelationshipDocument>()
            .TableName("content_relationship_definitions")
            .Identity(x => x.Id)
            .Index(x => x.TenantId)
            .Index(x => x.SiteId)
            .UniqueIndex(x => new { x.TenantId, x.SiteId, x.Alias });

        opts.Schema.For<ContentRelationshipDdlJournalDocument>()
            .TableName("content_relationship_ddl_journal")
            .Identity(x => x.Id)
            .Index(x => x.TenantId)
            .Index(x => x.SiteId)
            .Index(x => x.RelationshipId)
            .UniqueIndex(x => new { x.TenantId, x.SiteId, x.RelationshipId, x.AppliedSchemaFingerprint });

        opts.Schema.For<ContentSearchDocument>()
            .TableName(Schemas.Tables.ContentSearchIndex)
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.ContentTypeAlias)
            .Index(x => x.Culture)
            .Index(x => x.PublicationState)
            .FullTextIndex(
                x => x.FullText,
                ContentSearchConstants.AnalyzerName);

        opts.Schema.For<ContentSearchFacet>()
            .TableName(Schemas.Tables.ContentSearchFacets)
            .Identity(x => x.Id)
            .Index(x => x.ContentItemId)
            .Index(x => x.Culture)
            .Index(x => x.PublicationState)
            .Index(x => new
            {
                x.SiteId,
                x.ContentTypeAlias,
                x.FieldName,
                x.NormalizedValue
            });

        opts.Schema.For<ContentSemanticDocument>()
            .TableName(Schemas.Tables.ContentSemanticIndex)
            .Identity(x => x.Id)
            .Index(x => x.SiteId)
            .Index(x => x.ContentTypeAlias)
            .Index(x => x.Culture)
            .Index(x => x.PublicationState)
            .Index(x => x.ModelId)
            .HnswIndex(
                x => x.Embedding,
                ContentSearchConstants.VectorDimensions,
                Search.Distance.Cosine);
    }

        /// <summary>
    /// Applies the content schema through the service-aware store configuration contract.
    /// </summary>
    /// <param name="services">The service provider; not used.</param>
    /// <param name="opts">The store options to configure.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

        /// <summary>
    /// Maps the authenticated content-type and content-item administrative APIs.
    /// </summary>
    /// <param name="builder">The endpoint route builder to mutate.</param>
    /// <returns>A task already completed after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapContentTypesApi();
        builder.MapContentItemsApi();
        builder.MapContentHierarchyManagerApi();
        builder.MapContentViewsApi();
        builder.MapDynamicPageRoute<PublicContentRouteTransformer>("/{culture}/{typeAlias}/{entrySlug}");

        return Task.CompletedTask;
    }
}
