using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Content.Indexing;
using Aero.Cms.Core.Content.Jobs;
using Aero.Cms.Core.Content.Rendering;
using Aero.Cms.Core.Content.Services;
using Aero.Cms.Core.Extensions;
using Aero.Modular;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Content;

[Module(nameof(AeroContentModule))]
public sealed class AeroContentModule : AeroModuleBase, IContentDefinitionModule
{
    public override string Name => nameof(AeroContentModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override string Description => "Runtime-defined content types with Scriban-based rendering. " +
        "Managers define content type schemas (fields, validation, templates) at runtime. " +
        "Content items are stored as field bags (Dictionary<string, JsonElement>) and rendered " +
        "through the existing DynamicTemplateBlock pipeline.";

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["content", "infrastructure"];

    public override IReadOnlyList<string> Tags => ["content", "content-types", "cms", "structured-data"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Register the entire content type system via the extension method
        services.AddContentTypeSystem();
    }

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

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // Marten document configuration for the content type system
        opts.Schema.For<ContentTypeDocument>()
            .Identity(x => x.Id)
            .DocumentAlias("content_type_definitions")
            .Index(x => x.SiteId);

        opts.Schema.For<ContentItem>()
            .DocumentAlias("content_items")
            .Index(x => x.SiteId)
            .Index(x => x.Slug)
            .Index(x => x.ContentTypeAlias);

        opts.Schema.For<ContentItemVersion>()
            .DocumentAlias("content_item_versions")
            .Index(x => x.ContentItemId);
    }
}
