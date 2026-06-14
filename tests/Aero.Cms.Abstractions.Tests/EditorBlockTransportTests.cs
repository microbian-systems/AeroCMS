using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Abstractions.Tests;

public sealed class EditorBlockTransportTests
{
    [Test]
    public async Task SourceGeneratedTransportRoundTripsMixedPageComposition()
    {
        IReadOnlyList<EditorBlock> blocks =
        [
            new EditorBlock
            {
                EditorId = "hero",
                Type = "aero.hero.basic",
                MainText = "Welcome"
            },
            new EditorBlock
            {
                EditorId = "card",
                Type = "preset.card",
                CompositionNodes =
                [
                    new NeoPageNode
                    {
                        NodeId = "card-root",
                        CatalogId = "preset.card",
                        Kind = NeoPageNodeKind.Component,
                        Style = new ResponsiveNodeStyle
                        {
                            Base = new NodeStyle
                            {
                                Padding = new LogicalSpacing
                                {
                                    InlineStart = new CssLength(16, CssLengthUnit.Pixels)
                                }
                            },
                            Mobile = new NodeStyleOverride
                            {
                                Width = new CssLength(100, CssLengthUnit.Percent),
                                Direction = ContentDirection.RightToLeft
                            }
                        },
                        Children =
                        [
                            new NeoPageNode
                            {
                                NodeId = "title",
                                CatalogId = "primitive.text",
                                Kind = NeoPageNodeKind.Primitive,
                                Properties = new Dictionary<string, JsonElement>
                                {
                                    ["text"] = JsonSerializer.SerializeToElement("Card title")
                                }
                            }
                        ]
                    }
                ]
            }
        ];

        var json = JsonSerializer.Serialize(
            blocks,
            BlockJsonContext.Default.IReadOnlyListEditorBlock);
        var restored = JsonSerializer.Deserialize(
            json,
            BlockJsonContext.Default.IReadOnlyListEditorBlock);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!).Count().IsEqualTo(2);
        await Assert.That(restored[1].CompositionNodes[0].Children[0]
            .Properties["text"].GetString()).IsEqualTo("Card title");
        await Assert.That(restored[1].CompositionNodes[0].Style.Mobile!.Direction)
            .IsEqualTo(ContentDirection.RightToLeft);
        await Assert.That(restored[1].CompositionNodes[0].Style.Base.Padding.InlineStart)
            .IsEqualTo(new CssLength(16, CssLengthUnit.Pixels));
    }

    [Test]
    public async Task EditorBlockCloneKeepsCompositionSnapshotsIsolated()
    {
        var original = new EditorBlock
        {
            Type = "primitive.text",
            CompositionNodes =
            [
                new NeoPageNode
                {
                    NodeId = "root",
                    CatalogId = "primitive.text",
                    Kind = NeoPageNodeKind.Primitive,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["text"] = JsonSerializer.SerializeToElement("Original")
                    }
                }
            ]
        };

        var clone = original.DeepClone();
        clone.CompositionNodes[0].Properties["text"] =
            JsonSerializer.SerializeToElement("Changed");

        await Assert.That(original.CompositionNodes[0].Properties["text"].GetString())
            .IsEqualTo("Original");
        await Assert.That(clone.CompositionNodes[0].Properties["text"].GetString())
            .IsEqualTo("Changed");
    }
}
