using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Ui.Neo.Blocks.CenteredHero;
using Aero.Cms.Ui.Neo.Blocks.CtaBanner;
using Aero.Cms.Ui.Neo.Blocks.Hero;
using Aero.Cms.Ui.Neo.Blocks.Newsletter;
using Aero.Cms.Ui.Neo.Blocks.SplitHero;
using Aero.Cms.Ui.Neo.Blocks.StatsRow;
using Aero.Cms.Ui.Neo.Definitions;
using Aero.Cms.Ui.Neo.Primitives.Button;
using Aero.Cms.Ui.Neo.Primitives.Card;
using Aero.Cms.Ui.Neo.Primitives.Container;
using Aero.Cms.Ui.Neo.Primitives.Icon;
using Aero.Cms.Ui.Neo.Primitives.Image;
using Aero.Cms.Ui.Neo.Primitives.Pill;
using Aero.Cms.Ui.Neo.Primitives.Separator;
using Aero.Cms.Ui.Neo.Primitives.Text;

namespace Aero.Cms.Ui.Neo;

public sealed class NeoPageEditorBlockProvider :
    IPageEditorBlockProvider,
    IPageEditorDefinitionProvider,
    ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        new Hero01EditorBlockDefinition(),
        new CenteredHeroEditorBlockDefinition(),
        new SplitHeroEditorBlockDefinition(),
        new CtaBannerEditorBlockDefinition(),
        new NewsletterEditorBlockDefinition(),
        new StatsRowEditorBlockDefinition(),
        new BasicHeroEditorBlockDefinition(),
        new ImageEditorBlockDefinition(),
        new VideoEditorBlockDefinition(),
        new AudioEditorBlockDefinition(),
        new GalleryEditorBlockDefinition(),
        new NeoRawHtmlEditorBlockDefinition(),
        new SeparatorEditorBlockDefinition(),
        new NeoColumnsEditorBlockDefinition(),
        new ScribanEditorBlockDefinition()
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

    private static readonly IReadOnlyCollection<PageEditorDefinitionDescriptor> EditorDefinitions =
    [
        ContainerPrimitiveDefinition.Descriptor,
        TextPrimitiveDefinition.Descriptor,
        ButtonPrimitiveDefinition.Descriptor,
        ImagePrimitiveDefinition.Descriptor,
        PillPrimitiveDefinition.Descriptor,
        IconPrimitiveDefinition.Descriptor,
        SeparatorPrimitiveDefinition.Descriptor,
        CardPrimitiveDefinition.Descriptor
    ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<PageEditorDefinitionDescriptor> GetEditorDefinitions() => EditorDefinitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
