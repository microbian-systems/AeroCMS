using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Jobs;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
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
        services.AddScoped<IContentHierarchyQueryService, ContentHierarchyQueryService>();
        services.AddScoped<ContentHierarchyValidator>();
        services.AddScoped<ContentValidationService>();
        services.AddScoped<ContentCommandService>();
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
        services.AddSingleton<IContentFieldEditor, BooleanFieldEditor>();
        services.AddSingleton<IContentFieldEditor, UrlFieldEditor>();

        // Sync field validators
        services.AddSingleton<IContentFieldValidator, TextFieldValidator>();
        services.AddSingleton<IContentFieldValidator, NumberFieldValidator>();
        services.AddSingleton<IContentFieldValidator, ReferenceFieldValidator>();

        // Async validators (scoped — depend on scoped IContentService)
        services.AddScoped<IAsyncContentValidator, UniqueSlugValidator>();
        services.AddScoped<IAsyncContentValidator, ReferenceExistenceValidator>();

        // Scriban template snippets
        services.AddSingleton<IFieldTemplateSnippet, TextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, ImageFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, RichtextFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, UrlFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, NumberFieldSnippet>();
        services.AddSingleton<IFieldTemplateSnippet, BooleanFieldSnippet>();

        // Search indexers
        services.AddSingleton<IContentFieldIndexer, TextFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, RichTextFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, ReferenceFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, NumberFieldIndexer>();
        services.AddSingleton<IContentFieldIndexer, BooleanFieldIndexer>();

        // Background jobs
        services.AddScoped<ScheduledPublishHandler>();

        return services;
    }
}
