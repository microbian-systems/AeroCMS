using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core;
using Aero.Cms.Core.Content;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Content;

[Module(nameof(ContentModule))]
public sealed class ContentModule : AeroWebModule, IContentDefinitionModule
{
    public override string Name => nameof(ContentModule);

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

        // Grain-backed actors — direct injection for thin API controllers
        services.AddSingleton<IAeroContentItemActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentItemActor>(0, "aero"));
        services.AddSingleton<IAeroContentTypeActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroContentTypeActor>(0, "aero"));
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
            .DocumentAlias(Schemas.Tables.ContentTypes)
            .Index(x => x.SiteId)
            .UniqueIndex(x => x.SiteId, x => x.Alias);

        opts.Schema.For<ContentItem>()
            .DocumentAlias(Schemas.Tables.ContentItems)
            .Index(x => x.SiteId)
            .Index(x => x.Slug)
            .Index(x => x.ContentTypeAlias);

        opts.Schema.For<ContentItemVersion>()
            .DocumentAlias(Schemas.Tables.ContentItemVersions)
            .Index(x => x.ContentItemId);
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapContentTypesApi();
        builder.MapContentItemsApi();
        builder.MapBlocksApi();

        return Task.CompletedTask;
    }
}
