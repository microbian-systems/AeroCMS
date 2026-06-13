using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Ui.Neo.Blocks.CenteredHero;
using Aero.Cms.Ui.Neo.Blocks.CtaBanner;
using Aero.Cms.Ui.Neo.Blocks.Hero;
using Aero.Cms.Ui.Neo.Blocks.Newsletter;
using Aero.Cms.Ui.Neo.Blocks.SplitHero;
using Aero.Cms.Ui.Neo.Blocks.StatsRow;

namespace Aero.Cms.Ui.Neo;

public sealed class NeoPageEditorBlockProvider : IPageEditorBlockProvider, ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        new Hero01EditorBlockDefinition(),
        new CenteredHeroEditorBlockDefinition(),
        new SplitHeroEditorBlockDefinition(),
        new CtaBannerEditorBlockDefinition(),
        new NewsletterEditorBlockDefinition(),
        new StatsRowEditorBlockDefinition()

    ];

    private static readonly IReadOnlyCollection<CmsBlockModelRegistration> BlockModels =
    [
        new(Hero01Block.BlockTypeId,          typeof(Hero01Block)),
        new(CenteredHeroBlock.BlockTypeId,    typeof(CenteredHeroBlock)),
        new(SplitHeroBlock.BlockTypeId,       typeof(SplitHeroBlock)),
        new(CtaBannerBlock.BlockTypeId,       typeof(CtaBannerBlock)),
        new(NewsletterBlock.BlockTypeId,      typeof(NewsletterBlock)),
        new(StatsRowBlock.BlockTypeId,        typeof(StatsRowBlock))
    ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
