using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Jobs;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Search;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Content.Views;
using Aero.Cms.Core.Content.Templating;
using Aero.Core.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Core.Extensions;

/// <summary>
/// Extension methods for registering the content type system services with DI.
/// </summary>
public static class ContentServiceExtensions
{
    /// <summary>
    /// Registers all content type system services, editors, validators,
    /// template snippets, indexers, and background jobs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Content persistence, query, validation, command, rendering, asynchronous validators,
    /// and scheduled publishing are scoped. Editors, synchronous validators, snippets, and
    /// indexers are singleton registrations. The sanitizer, template options, validator, and
    /// secure renderer use try-add singleton registration and therefore preserve an existing
    /// registration for the same service type.
    ///
    /// Calling this method more than once is not idempotent: registrations made with
    /// <c>AddScoped</c> or <c>AddSingleton</c>, particularly enumerable extension points, are
    /// appended again.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddContentTypeSystem(this IServiceCollection services)
    {
        // Core services
        services.AddScoped<AeroContentTypeService>();
        services.AddScoped<IContentTypeService>(
            static provider => provider.GetRequiredService<AeroContentTypeService>());
        services.AddScoped<AeroContentService>();
        services.AddScoped<IContentService>(
            static provider => provider.GetRequiredService<AeroContentService>());
        services.AddScoped<IContentQueryService, AeroContentQueryService>();
        services.AddScoped<IContentSurrealViewService, ContentSurrealViewService>();
        services.AddScoped<IContentSurrealViewStore, SableContentSurrealViewStore>();
        services.AddScoped<IContentRelationshipStore, SableContentRelationshipStore>();
        services.AddScoped<IContentEntrySourceProviderCatalog, ContentSurrealViewEntryProviderCatalog>();
        services.TryAddSingleton<IContentPhysicalSchemaTargetRegistry, EmptyContentPhysicalSchemaTargetRegistry>();
        services.TryAddSingleton<IPrivilegedContentSchemaCommandExecutor, DisabledContentSchemaCommandExecutor>();
        services.TryAddSingleton<IContentRelationshipSchemaApplyCoordinator, DisabledContentRelationshipSchemaApplyCoordinator>();
        services.AddScoped<IContentSchemaMetadataReader, SableContentSchemaMetadataReader>();
        services.AddScoped<IContentRelationshipSchemaDiscovery, SableContentRelationshipSchemaDiscovery>();
        services.AddScoped<IRelationshipDdlLifecycle, ContentRelationshipDdlLifecycle>();
        services.TryAddSingleton<IContentViewScopeBinder, ReservedContentViewScopeBinder>();
        services.TryAddSingleton<IContentShapeRegistry, ContentShapeRegistry>();
        services.TryAddSingleton<IContentViewSourceRegistry, ContentViewSourceRegistry>();
        services.TryAddSingleton<IContentViewStatementClassifier, SurrealSelectStatementClassifier>();
        services.TryAddSingleton<IContentViewTrustedQueryPlanRegistry, EmptyContentViewTrustedQueryPlanRegistry>();
        services.TryAddSingleton<IContentViewRelationshipPlanDialectCapability, DisabledContentViewRelationshipPlanDialectCapability>();
        services.TryAddSingleton<IReadOnlyContentViewExecutor, DisabledContentViewExecutor>();
        services.TryAddSingleton<IAdminReadOnlyContentViewExecutor, DisabledAdminContentViewExecutor>();
        services.TryAddSingleton<InMemoryContentViewCache>();
        services.TryAddSingleton<IContentViewExecutionCache>(sp => sp.GetRequiredService<InMemoryContentViewCache>());
        services.TryAddSingleton<IContentViewCacheInvalidator>(sp => sp.GetRequiredService<InMemoryContentViewCache>());
        services.TryAddSingleton<IContentViewCacheGenerationProvider>(sp => sp.GetRequiredService<InMemoryContentViewCache>());
        services.TryAddSingleton<IContentViewDistributedCacheCoordinator, DisabledContentViewDistributedCacheCoordinator>();
        services.TryAddSingleton<IContentViewOutputCacheInvalidator, DisabledContentViewOutputCacheInvalidator>();
        services.AddScoped<IContentHierarchyQueryService, ContentHierarchyQueryService>();
        services.AddScoped<ContentHierarchyValidator>();
        services.AddScoped<ContentValidationService>();
        services.AddScoped<ContentCommandService>();
        services.AddScoped<IContentLocalizationHandler, ContentLocalizationHandler>();
        services.AddScoped<ContentIndexService>();
        services.AddScoped<ContentSearchProjectionService>();
        services.TryAddSingleton<IContentEmbeddingGenerator, UnavailableContentEmbeddingGenerator>();
        services.AddScoped<IContentItemRenderer, ContentItemRenderer>();
        services.TryAddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        services.TryAddSingleton<SecureScribanTemplateOptions>();
        services.TryAddSingleton<ScribanTemplateValidator>();
        services.TryAddSingleton<ISecureScribanRenderer, SecureScribanRenderer>();

        // Field editors (admin UI)
        services.AddSingleton<IContentFieldEditor, TextFieldEditor>();
        services.AddSingleton<IContentFieldEditor, ImageFieldEditor>();
        services.AddSingleton<IContentFieldEditor, RichtextFieldEditor>();
        services.AddSingleton<IContentFieldEditor, NumberFieldEditor>();
        services.AddSingleton<IContentFieldEditor, RangeFieldEditor>();
        services.AddSingleton<IContentFieldEditor, ColorFieldEditor>();
        services.AddSingleton<IContentFieldEditor, BooleanFieldEditor>();
        services.AddSingleton<IContentFieldEditor, UrlFieldEditor>();

        // Sync field validators
        services.AddSingleton<IContentFieldValidator, TextFieldValidator>();
        services.AddSingleton<IContentFieldValidator, NumberFieldValidator>();
        services.AddSingleton<IContentFieldValidator, RangeFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ColorFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ReferenceFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ListFieldValidator>();
        services.AddSingleton<IContentFieldValidator, GalleryFieldValidator>();
        services.AddSingleton<IContentFieldValidator, DictionaryFieldValidator>();

        // Async validators (scoped — depend on scoped IContentService)
        services.AddScoped<IAsyncContentValidator, UniqueSlugValidator>();
        services.AddScoped<IAsyncContentValidator, ReferenceExistenceValidator>();

        // Scriban template snippets
        services.AddSingleton<IFieldTemplateSnippet, TextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ImageFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, RichtextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, UrlFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, NumberFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, RangeFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ColorFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, BooleanFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ListFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, GalleryFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, DictionaryFieldSnippet>();

        // Search indexers
        services.AddSingleton<IContentFieldIndexer, TextFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, UrlFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, RichTextFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, ReferenceFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, NumberFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, RangeFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, ColorFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, BooleanFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, ListFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, DictionaryFieldIndexer>();

        // Background jobs
        services.AddScoped<ScheduledPublishHandler>();

        return services;
    }
}
