using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Modules;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Pages;

public sealed class PagesModule : AeroModuleBase
{
    public override string Name => nameof(PagesModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content", "pages"];
    public override IReadOnlyList<string> Tags => ["content", "pages", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IPageContentService, MartenPageContentService>();
        services.AddSingleton<IConfigureMarten, BlockMartenConfiguration>();
    }

    public override Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        // MapPageRoutes() removed - now using Razor Pages for dynamic page rendering
        // Routes are handled by Areas/Cms/Pages/Page.cshtml with @page "/{slug?}"
        return Task.CompletedTask;
    }
}

internal sealed class BlockMartenConfiguration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Schema.For<BlockBase>().AddSubClassHierarchy(
            typeof(RichTextBlock),
            typeof(HeadingBlock),
            typeof(ImageBlock),
            typeof(CtaBlock),
            typeof(QuoteBlock),
            typeof(EmbedBlock));
    }
}
