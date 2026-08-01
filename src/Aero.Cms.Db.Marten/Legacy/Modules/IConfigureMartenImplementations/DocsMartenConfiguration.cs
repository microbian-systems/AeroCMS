namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Represents a class for DocsMartenConfiguration.
/// </summary>
public sealed class DocsMartenConfiguration : IConfigureMarten
{
        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions options)
    {
        // Marten will manage MarkdownPage in its own table.
        options.Schema.For<DocsPage>().Index(x => x.Slug);
        options.Schema.For<DocsPage>().Index(x => x.Culture);
        options.Schema.For<DocsPage>().Index(x => x.TranslationGroupId);
    }
}
