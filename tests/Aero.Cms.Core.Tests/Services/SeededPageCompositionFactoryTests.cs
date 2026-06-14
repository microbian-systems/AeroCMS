using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Modules.Setup;

namespace Aero.Cms.Core.Tests.Services;

public sealed class SeededPageCompositionFactoryTests
{
    [Test]
    public async Task Creates_responsive_bidirectional_composition()
    {
        var root = SeededPageCompositionFactory.CreateBidirectionalFeature();

        await Assert.That(root.CatalogId).IsEqualTo("primitive.container");
        await Assert.That(root.Children.Count).IsEqualTo(3);
        await Assert.That(root.Style.Base.Direction)
            .IsEqualTo(ContentDirection.LeftToRight);
        await Assert.That(root.Style.Resolve(EditorBreakpoint.Mobile)
                .Padding.InlineStart)
            .IsEqualTo(new CssLength(1, CssLengthUnit.Rem));
        await Assert.That(root.Children[0].Style.Base.Direction)
            .IsEqualTo(ContentDirection.LeftToRight);
        await Assert.That(root.Children[1].Style.Base.Direction)
            .IsEqualTo(ContentDirection.RightToLeft);
        await Assert.That(root.Children[1].Properties["text"].GetString())
            .Contains("أنشئ");
    }

    [Test]
    public async Task Creates_fresh_node_identities_for_each_seed_instance()
    {
        var first = SeededPageCompositionFactory.CreateBidirectionalFeature();
        var second = SeededPageCompositionFactory.CreateBidirectionalFeature();

        await Assert.That(first.NodeId).IsNotEqualTo(second.NodeId);
        await Assert.That(first.Children.Select(child => child.NodeId))
            .DoesNotContain(second.Children[0].NodeId);
    }
}
