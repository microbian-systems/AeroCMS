using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Marten;

namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Central Marten configuration for CMS block polymorphic serialization.
/// </summary>
public sealed class BlockMartenConfiguration : IConfigureMarten
{
    public void Configure(IServiceProvider services, StoreOptions options)
    {
        options.Schema.For<BlockBase>().AddSubClassHierarchy(
            typeof(RichTextBlock),
            typeof(HeadingBlock),
            typeof(ImageBlock),
            typeof(CtaBlock),
            typeof(QuoteBlock),
            typeof(EmbedBlock),
            typeof(NavigationBlock),
            typeof(RawHtmlBlock),
            typeof(AeroAuthBlock),
            typeof(AeroBlogBlock),
            typeof(AeroContactBlock),
            typeof(AeroCtaBlock),
            typeof(AeroFaqBlock),
            typeof(AeroFeaturesBlock),
            typeof(AeroHeroBlock),
            typeof(AeroPortfolioBlock),
            typeof(AeroPricingBlock),
            typeof(AeroTableBlock),
            typeof(AeroTeamsBlock),
            typeof(AeroTestimonialsBlock));
    }
}
