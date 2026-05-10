using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Blocks.Dynamic;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Jobs;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Extensions;

/// <summary>
/// Extension methods for registering the content type system services with DI.
/// </summary>
public static class ContentServiceExtensions
{
    /// <summary>
    /// Registers all content type system services, bridges, editors, validators,
    /// template snippets, indexers, and background jobs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddContentTypeSystem(this IServiceCollection services)
    {
        // Core services
        services.AddScoped<IContentTypeService, MartenContentTypeService>();
        services.AddScoped<IContentService, MartenContentService>();
        services.AddScoped<IContentQueryService, MartenContentQueryService>();
        services.AddScoped<ContentValidationService>();
        services.AddScoped<ContentCommandService>();
        services.AddScoped<ContentEmbedBlockRenderer>();

        // Rendering bridge (scoped — depends on IDocumentSession)
        services.AddScoped<IContentTypeRenderingBridge, ContentTypeDynamicBlockBridge>();

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
