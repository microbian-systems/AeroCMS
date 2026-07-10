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
using Aero.Cms.Ui.Neo.Primitives.Article;
using Aero.Cms.Ui.Neo.Primitives.Aside;
using Aero.Cms.Ui.Neo.Primitives.Blockquote;
using Aero.Cms.Ui.Neo.Primitives.Audio;
using Aero.Cms.Ui.Neo.Primitives.Auth;
using Aero.Cms.Ui.Neo.Primitives.Blog;
using Aero.Cms.Ui.Neo.Primitives.BoringHero;
using Aero.Cms.Ui.Neo.Primitives.Card;
using Aero.Cms.Ui.Neo.Primitives.Carousel;
using Aero.Cms.Ui.Neo.Primitives.Code;
using Aero.Cms.Ui.Neo.Primitives.Contact;
using Aero.Cms.Ui.Neo.Primitives.Cta;
using Aero.Cms.Ui.Neo.Primitives.Faq;
using Aero.Cms.Ui.Neo.Primitives.Features;
using Aero.Cms.Ui.Neo.Primitives.Gallery;
using Aero.Cms.Ui.Neo.Primitives.Hero;
using Aero.Cms.Ui.Neo.Primitives.Portfolio;
using Aero.Cms.Ui.Neo.Primitives.Pricing;
using Aero.Cms.Ui.Neo.Primitives.Quote;
using Aero.Cms.Ui.Neo.Primitives.RawHtml;
using Aero.Cms.Ui.Neo.Primitives.Table;
using Aero.Cms.Ui.Neo.Primitives.Teams;
using Aero.Cms.Ui.Neo.Primitives.Testimonials;
using Aero.Cms.Ui.Neo.Primitives.Video;
using Aero.Cms.Ui.Neo.Primitives.Container;
using Aero.Cms.Ui.Neo.Primitives.Embed;
using Aero.Cms.Ui.Neo.Primitives.Footer;
using Aero.Cms.Ui.Neo.Primitives.Form;
using Aero.Cms.Ui.Neo.Primitives.Grid;
using Aero.Cms.Ui.Neo.Primitives.Header;
using Aero.Cms.Ui.Neo.Primitives.Heading;
using Aero.Cms.Ui.Neo.Primitives.Icon;
using Aero.Cms.Ui.Neo.Primitives.Image;
using Aero.Cms.Ui.Neo.Primitives.Nav;
using Aero.Cms.Ui.Neo.Primitives.Pill;
using Aero.Cms.Ui.Neo.Primitives.Section;
using Aero.Cms.Ui.Neo.Primitives.Columns;
using Aero.Cms.Ui.Neo.Primitives.Content;
using Aero.Cms.Ui.Neo.Primitives.CssGrid;
using Aero.Cms.Ui.Neo.Primitives.DynamicTemplate;
using Aero.Cms.Ui.Neo.Primitives.Flexbox;
using Aero.Cms.Ui.Neo.Primitives.MarkdownBlock;
using Aero.Cms.Ui.Neo.Primitives.Separator;
using Aero.Cms.Ui.Neo.Primitives.Text;
using Aero.Cms.Ui.Neo.Primitives.TextBlock;

namespace Aero.Cms.Ui.Neo;

/// <summary>
/// Represents a class for NeoPageEditorBlockProvider.
/// </summary>
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
        FlexboxPrimitiveDefinition.Descriptor,
        CssGridPrimitiveDefinition.Descriptor,
        TextPrimitiveDefinition.Descriptor,
        HeadingPrimitiveDefinition.Descriptor,
        ButtonPrimitiveDefinition.Descriptor,
        ImagePrimitiveDefinition.Descriptor,
        PillPrimitiveDefinition.Descriptor,
        BlockquotePrimitiveDefinition.Descriptor,
        IconPrimitiveDefinition.Descriptor,
        SeparatorPrimitiveDefinition.Descriptor,
        CodePrimitiveDefinition.Descriptor,
        EmbedPrimitiveDefinition.Descriptor,
        SectionPrimitiveDefinition.Descriptor,
        ArticlePrimitiveDefinition.Descriptor,
        NavPrimitiveDefinition.Descriptor,
        HeaderPrimitiveDefinition.Descriptor,
        FooterPrimitiveDefinition.Descriptor,
        FormPrimitiveDefinition.Descriptor,
        AsidePrimitiveDefinition.Descriptor,
        CardPrimitiveDefinition.Descriptor,
        GridDefinition.Descriptor,
        GridRowDefinition.Descriptor,
        GridCellDefinition.Descriptor,
        HeroPrimitiveDefinition.Descriptor,
        FeaturesPrimitiveDefinition.Descriptor,
        PricingPrimitiveDefinition.Descriptor,
        CtaPrimitiveDefinition.Descriptor,
        FaqPrimitiveDefinition.Descriptor,
        TestimonialsPrimitiveDefinition.Descriptor,
        BlogPrimitiveDefinition.Descriptor,
        TeamsPrimitiveDefinition.Descriptor,
        ContactPrimitiveDefinition.Descriptor,
        PortfolioPrimitiveDefinition.Descriptor,
        TablePrimitiveDefinition.Descriptor,
        AuthPrimitiveDefinition.Descriptor,
        BoringHeroPrimitiveDefinition.Descriptor,
        RawHtmlPrimitiveDefinition.Descriptor,
        QuotePrimitiveDefinition.Descriptor,
        VideoPrimitiveDefinition.Descriptor,
        AudioPrimitiveDefinition.Descriptor,
        GalleryPrimitiveDefinition.Descriptor,
        CarouselPrimitiveDefinition.Descriptor,
        ContentPrimitiveDefinition.Descriptor,
        TextBlockPrimitiveDefinition.Descriptor,
        MarkdownBlockPrimitiveDefinition.Descriptor,
        DynamicTemplatePrimitiveDefinition.Descriptor,
        ColumnsPrimitiveDefinition.Descriptor
    ];

        /// <summary>
    /// GetDefinitions method.
    /// </summary>
public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
        /// <summary>
    /// GetEditorDefinitions method.
    /// </summary>
public IReadOnlyCollection<PageEditorDefinitionDescriptor> GetEditorDefinitions() => EditorDefinitions;
        /// <summary>
    /// GetBlockModels method.
    /// </summary>
public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
