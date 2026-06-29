using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Embed;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Ui.Neo.Embed;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Ui.Neo;

public static class NeoUiServiceCollectionExtensions
{
    public static IServiceCollection AddAeroCmsNeoUiBlocks(this IServiceCollection services)
    {
        var provider = new NeoPageEditorBlockProvider();
        services.AddSingleton<IPageEditorBlockProvider>(provider);
        services.AddSingleton<IPageEditorDefinitionProvider>(provider);
        services.AddSingleton<ICmsBlockModelProvider>(provider);
        services.AddSingleton<ICmsBlockRenderRegistry, NeoCmsBlockRenderRegistry>();
        services.AddSingleton<ICmsBlockRenderRegistry, GeneratedCmsBlockRenderRegistry>();

        // Embed resolver pipeline — strategies tried in registration order
        services.AddSingleton<IEmbedUrlResolver, YouTubeEmbedResolver>();
        services.AddSingleton<IEmbedUrlResolver, VimeoEmbedResolver>();
        services.AddSingleton<IEmbedUrlResolver, GoogleMapsEmbedResolver>();
        services.AddSingleton<IEmbedUrlResolver, CalendlyEmbedResolver>();
        services.AddSingleton<IEmbedUrlResolver, GenericIframeResolver>();
        services.AddSingleton<EmbedResolverPipeline>();
        services.AddSingleton<EmbedAllowList>();

        return services;
    }
}
