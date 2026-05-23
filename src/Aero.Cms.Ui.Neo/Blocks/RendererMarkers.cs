using Aero.Cms.Abstractions.Blocks.Rendering;

// Phase 1 — Hero & Marketing
namespace Aero.Cms.Ui.Neo.Blocks.CenteredHero
{
    [CmsBlockRenderer(typeof(CenteredHeroBlock))] public partial class CenteredHeroBlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.Hero
{
    [CmsBlockRenderer(typeof(Hero01Block))] public partial class Hero01BlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.Newsletter
{
    [CmsBlockRenderer(typeof(NewsletterBlock))] public partial class NewsletterBlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.SplitHero
{
    [CmsBlockRenderer(typeof(SplitHeroBlock))] public partial class SplitHeroBlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.CtaBanner
{
    [CmsBlockRenderer(typeof(CtaBannerBlock))] public partial class CtaBannerBlockRenderer;
}

namespace Aero.Cms.Ui.Neo.Blocks.StatsRow
{
    [CmsBlockRenderer(typeof(StatsRowBlock))] public partial class StatsRowBlockRenderer;
}
