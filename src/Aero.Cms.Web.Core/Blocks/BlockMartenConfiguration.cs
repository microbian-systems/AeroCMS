using Aero.Cms.Core.Blocks;
using Marten;

namespace Aero.Cms.Web.Core.Blocks;

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
            typeof(NavigationBlock));
    }
}
