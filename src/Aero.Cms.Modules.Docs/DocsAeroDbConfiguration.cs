namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Adds secondary indexes used to find documentation pages by route and translation group.
/// </summary>
/// <remarks>
/// This configuration does not enable optimistic concurrency or declare the site/culture/slug
/// uniqueness constraint configured by <see cref="DocsModule"/>.
/// </remarks>
public sealed class DocsAeroDbConfiguration : IConfigureAeroDB
{
    /// <summary>
    /// Adds slug, culture, and translation-group indexes to the document schema.
    /// </summary>
    /// <param name="options">The mutable store configuration.</param>
public void Configure(StoreOptions options)
    {
        // AeroDB will manage MarkdownPage in its own table.
        options.Schema.For<DocsPage>().Index(x => x.Slug);
        options.Schema.For<DocsPage>().Index(x => x.Culture);
        options.Schema.For<DocsPage>().Index(x => x.TranslationGroupId);
    }

    /// <summary>
    /// Applies the same schema configuration when a service provider is available.
    /// </summary>
    /// <param name="services">The application service provider; this implementation does not use it.</param>
    /// <param name="options">The mutable store configuration.</param>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }
}
