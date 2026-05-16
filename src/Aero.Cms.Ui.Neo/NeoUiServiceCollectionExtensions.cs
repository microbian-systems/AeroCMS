using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Ui.Neo;

public static class NeoUiServiceCollectionExtensions
{
    public static IServiceCollection AddAeroCmsNeoUiBlocks(this IServiceCollection services)
    {
        var provider = new NeoPageEditorBlockProvider();
        PageEditorBlockRegistry.RegisterProviders([provider]);
        services.AddSingleton<IPageEditorBlockProvider>(provider);
        services.AddSingleton<ICmsBlockModelProvider>(provider);
        services.AddSingleton<ICmsBlockRenderRegistry, GeneratedCmsBlockRenderRegistry>();
        return services;
    }
}
