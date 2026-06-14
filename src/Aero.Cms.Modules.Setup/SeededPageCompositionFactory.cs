using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Modules.Setup;

internal static class SeededPageCompositionFactory
{
    public static NeoPageNode CreateBidirectionalFeature()
    {
        var container = Node(
            "primitive.container",
            NeoPageNodeKind.Container,
            style: new ResponsiveNodeStyle
            {
                Base = new NodeStyle
                {
                    Direction = ContentDirection.LeftToRight,
                    MaximumWidth = new CssLength(72, CssLengthUnit.Rem),
                    Margin = new LogicalSpacing
                    {
                        BlockStart = new CssLength(3, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(3, CssLengthUnit.Rem),
                        InlineStart = CssLength.Auto,
                        InlineEnd = CssLength.Auto
                    },
                    Padding = new LogicalSpacing
                    {
                        BlockStart = new CssLength(2, CssLengthUnit.Rem),
                        BlockEnd = new CssLength(2, CssLengthUnit.Rem),
                        InlineStart = new CssLength(2, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(2, CssLengthUnit.Rem)
                    }
                },
                Mobile = new NodeStyleOverride
                {
                    Padding = new LogicalSpacingOverride
                    {
                        InlineStart = new CssLength(1, CssLengthUnit.Rem),
                        InlineEnd = new CssLength(1, CssLengthUnit.Rem)
                    }
                }
            });

        container.Children.Add(Text(
            "Build once. Adapt everywhere.",
            ContentDirection.LeftToRight));
        container.Children.Add(Text(
            "أنشئ مرة واحدة، وقدّم تجربة ممتازة في كل اتجاه.",
            ContentDirection.RightToLeft));
        container.Children.Add(Node(
            "primitive.button",
            NeoPageNodeKind.Primitive,
            new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("Explore Aero CMS"),
                ["url"] = JsonSerializer.SerializeToElement("/blog")
            }));

        return container;
    }

    private static NeoPageNode Text(string text, ContentDirection direction) =>
        Node(
            "primitive.text",
            NeoPageNodeKind.Primitive,
            new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text)
            },
            new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Direction = direction }
            });

    private static NeoPageNode Node(
        string catalogId,
        NeoPageNodeKind kind,
        Dictionary<string, JsonElement>? properties = null,
        ResponsiveNodeStyle? style = null) =>
        new()
        {
            NodeId = Guid.NewGuid().ToString("N"),
            CatalogId = catalogId,
            Kind = kind,
            Properties = properties ?? [],
            Style = style ?? new ResponsiveNodeStyle()
        };
}
