using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

        // Register this assembly so the Razor Pages in Areas/Cms/Pages are discovered
        services.AddRazorPages()
            .AddApplicationPart(typeof(PagesModule).Assembly);

        // Map area page routes — without this, pages in Areas/Cms/Pages/ are only
        // reachable via the area-prefixed default (e.g. /Cms/Page). These conventions
        // expose them at the desired public URLs.
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Cms", "/Page", "/{slug?}");
        });
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
