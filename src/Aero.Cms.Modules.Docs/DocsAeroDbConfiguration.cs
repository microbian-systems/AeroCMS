namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Represents a class for DocsAeroDbConfiguration.
/// </summary>
public sealed class DocsAeroDbConfiguration : IConfigureAeroDB
{
        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions options)
    {
        // AeroDB will manage MarkdownPage in its own table.
        options.Schema.For<DocsPage>().Index(x => x.Slug);
        options.Schema.For<DocsPage>().Index(x => x.Culture);
        options.Schema.For<DocsPage>().Index(x => x.TranslationGroupId);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }
}
