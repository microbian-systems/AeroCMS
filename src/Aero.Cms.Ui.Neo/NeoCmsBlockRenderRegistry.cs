using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Shared.Blocks.Rendering;
using Aero.Cms.Ui.Neo.Blocks.CenteredHero;
using Aero.Cms.Ui.Neo.Blocks.CtaBanner;
using Aero.Cms.Ui.Neo.Blocks.Hero;
using Aero.Cms.Ui.Neo.Blocks.Newsletter;
using Aero.Cms.Ui.Neo.Blocks.SplitHero;
using Aero.Cms.Ui.Neo.Blocks.StatsRow;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Ui.Neo;

/// <summary>
/// Explicit DI render registry for NeoUI blocks. This keeps public rendering aligned
/// with the palette catalog even when source-generated registry output changes.
/// </summary>
public sealed class NeoCmsBlockRenderRegistry : ICmsBlockRenderRegistry
{
    private static readonly IReadOnlyDictionary<string, ICmsBlockRenderAdapter> Adapters = CreateAdapters();

        /// <summary>
    /// TryGet method.
    /// </summary>
public bool TryGet(string blockType, out ICmsBlockRenderAdapter adapter) =>
        Adapters.TryGetValue(blockType, out adapter!);

    private static IReadOnlyDictionary<string, ICmsBlockRenderAdapter> CreateAdapters()
    {
        var adapters = new Dictionary<string, ICmsBlockRenderAdapter>(StringComparer.OrdinalIgnoreCase);

        Add<CenteredHeroBlock, CenteredHeroBlockRenderer>(adapters, CenteredHeroBlock.BlockTypeId);
        Add<Hero01Block, Hero01BlockRenderer>(adapters, Hero01Block.BlockTypeId);
        Add<NewsletterBlock, NewsletterBlockRenderer>(adapters, NewsletterBlock.BlockTypeId);
        Add<SplitHeroBlock, SplitHeroBlockRenderer>(adapters, SplitHeroBlock.BlockTypeId);
        Add<CtaBannerBlock, CtaBannerBlockRenderer>(adapters, CtaBannerBlock.BlockTypeId);
        Add<StatsRowBlock, StatsRowBlockRenderer>(adapters, StatsRowBlock.BlockTypeId);

        return adapters;
    }

    private static void Add<TBlock, TComponent>(
        Dictionary<string, ICmsBlockRenderAdapter> adapters,
        string blockType)
        where TBlock : BlockBase
        where TComponent : IComponent
    {
        adapters[blockType] = new ComponentCmsBlockRenderAdapter<TBlock, TComponent>(blockType);
    }
}
