using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Shared.Blocks.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Ui.Hyper;

/// <summary>
/// Represents a class for HyperUiServiceCollectionExtensions.
/// </summary>
public static class HyperUiServiceCollectionExtensions
{
        /// <summary>
    /// AddAeroCmsHyperUiBlocks method.
    /// </summary>
public static IServiceCollection AddAeroCmsHyperUiBlocks(this IServiceCollection services)
    {
        var provider = new HyperPageEditorBlockProvider();
        services.AddSingleton<IPageEditorBlockProvider>(provider);
        services.AddSingleton<ICmsBlockModelProvider>(provider);
        services.AddSingleton<ICmsBlockRenderRegistry, HyperCmsBlockRenderRegistry>();
        services.AddSingleton<ICmsBlockRenderRegistry, GeneratedCmsBlockRenderRegistry>();
        return services;
    }
}
