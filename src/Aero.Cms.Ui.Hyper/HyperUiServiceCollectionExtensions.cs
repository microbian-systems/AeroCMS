using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Shared.Blocks.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Ui.Hyper;

public static class HyperUiServiceCollectionExtensions
{
    public static IServiceCollection AddAeroCmsHyperUiBlocks(this IServiceCollection services)
    {
        var provider = new HyperPageEditorBlockProvider();
        services.AddSingleton<IPageEditorBlockProvider>(provider);
        services.AddSingleton<ICmsBlockModelProvider>(provider);
        services.AddSingleton<ICmsBlockRenderRegistry, GeneratedCmsBlockRenderRegistry>();
        return services;
    }
}
