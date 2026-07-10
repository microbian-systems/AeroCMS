namespace Aero.Cms.Modules.Docs;

public sealed class DocsAeroDbConfiguration : IConfigureAeroDB
{
    public void Configure(StoreOptions options)
    {
        // AeroDB will manage MarkdownPage in its own table.
        options.Schema.For<DocsPage>().Index(x => x.Slug);
        options.Schema.For<DocsPage>().Index(x => x.Culture);
        options.Schema.For<DocsPage>().Index(x => x.TranslationGroupId);
    }

    public void Configure(IServiceProvider services, StoreOptions options)
    {
        Configure(options);
    }
}
