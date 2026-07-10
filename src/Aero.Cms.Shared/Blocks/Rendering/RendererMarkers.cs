using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Rendering;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Represents a class for RichTextBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(RichTextBlock))]
public partial class RichTextBlockRenderer;

/// <summary>
/// Represents a class for HeadingBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(HeadingBlock))]
public partial class HeadingBlockRenderer;

/// <summary>
/// Represents a class for CtaBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(CtaBlock))]
public partial class CtaBlockRenderer;

/// <summary>
/// Represents a class for QuoteBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(QuoteBlock))]
public partial class QuoteBlockRenderer;

/// <summary>
/// Represents a class for EmbedBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(EmbedBlock))]
public partial class EmbedBlockRenderer;

#if !AERO_CMS_BROWSER_CLIENT
/// <summary>
/// Represents a class for DynamicTemplateBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(DynamicTemplateBlock))]
public partial class DynamicTemplateBlockRenderer;
#endif

/// <summary>
/// Represents a class for ColumnsRenderer.
/// </summary>
[CmsBlockRenderer(typeof(Aero.Cms.Abstractions.Blocks.Common.ColumnsBlock))]
public partial class ColumnsRenderer;

/// <summary>
/// Represents a class for BoringHeroRenderer.
/// </summary>
[CmsBlockRenderer(typeof(BoringHeroBlock))]
public partial class BoringHeroRenderer;

/// <summary>
/// Represents a class for HeroRenderer.
/// </summary>
[CmsBlockRenderer(typeof(HeroBlock))]
public partial class HeroRenderer;

/// <summary>
/// Represents a class for AeroHeroRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroHeroBlock))]
public partial class AeroHeroRenderer;

/// <summary>
/// Represents a class for AeroFeaturesRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroFeaturesBlock))]
public partial class AeroFeaturesRenderer;

/// <summary>
/// Represents a class for AeroCtaRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroCtaBlock))]
public partial class AeroCtaRenderer;

/// <summary>
/// Represents a class for AeroBlogRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroBlogBlock))]
public partial class AeroBlogRenderer;

/// <summary>
/// Represents a class for AeroPricingRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroPricingBlock))]
public partial class AeroPricingRenderer;

/// <summary>
/// Represents a class for AeroTeamsRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroTeamsBlock))]
public partial class AeroTeamsRenderer;

/// <summary>
/// Represents a class for AeroTestimonialsRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroTestimonialsBlock))]
public partial class AeroTestimonialsRenderer;

/// <summary>
/// Represents a class for AeroFaqRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroFaqBlock))]
public partial class AeroFaqRenderer;

/// <summary>
/// Represents a class for AeroPortfolioRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroPortfolioBlock))]
public partial class AeroPortfolioRenderer;

/// <summary>
/// Represents a class for AeroContactRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroContactBlock))]
public partial class AeroContactRenderer;

/// <summary>
/// Represents a class for AeroTableRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroTableBlock))]
public partial class AeroTableRenderer;

/// <summary>
/// Represents a class for AeroAuthRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AeroAuthBlock))]
public partial class AeroAuthRenderer;

/// <summary>
/// Represents a class for CarouselRenderer.
/// </summary>
[CmsBlockRenderer(typeof(CarouselBlock))]
public partial class CarouselRenderer;

/// <summary>
/// Represents a class for NeoCompositionBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(NeoCompositionBlock))]
public partial class NeoCompositionBlockRenderer;

/// <summary>
/// Represents a class for ImageBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(Aero.Cms.Abstractions.Blocks.Neo.ImageBlock))]
public partial class ImageBlockRenderer;

/// <summary>
/// Represents a class for ConcreteImageBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(Aero.Cms.Abstractions.Blocks.ImageBlock))]
public partial class ConcreteImageBlockRenderer;

/// <summary>
/// Represents a class for VideoBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(VideoBlock))]
public partial class VideoBlockRenderer;

/// <summary>
/// Represents a class for AudioBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(AudioBlock))]
public partial class AudioBlockRenderer;

/// <summary>
/// Represents a class for GalleryBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(GalleryBlock))]
public partial class GalleryBlockRenderer;

/// <summary>
/// Represents a class for RawHtmlBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(NeoRawHtmlBlock))]
public partial class RawHtmlBlockRenderer;

/// <summary>
/// Represents a class for SeparatorBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(SeparatorBlock))]
public partial class SeparatorBlockRenderer;

/// <summary>
/// Represents a class for ColumnsBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(Aero.Cms.Abstractions.Blocks.Neo.NeoColumnsBlock))]
public partial class ColumnsBlockRenderer;

#if !AERO_CMS_BROWSER_CLIENT
/// <summary>
/// Represents a class for ScribanBlockRenderer.
/// </summary>
[CmsBlockRenderer(typeof(ScribanBlock))]
public partial class ScribanBlockRenderer;
#endif
