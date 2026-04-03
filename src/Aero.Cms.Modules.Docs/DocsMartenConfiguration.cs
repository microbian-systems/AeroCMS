using Aero.Cms.Core.Entities;
using Marten;

namespace Aero.Cms.Modules.Docs;

public sealed class DocsMartenConfiguration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        // Marten will manage MarkdownPage in its own table.
        options.Schema.For<DocsPage>().Index(x => x.Slug);
    }
}
