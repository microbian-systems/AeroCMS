using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;

namespace Aero.Cms.Abstractions.Tests;

public sealed class NodeEditorSessionTests
{
    [Test]
    public async Task Working_copy_does_not_mutate_original_node()
    {
        var original = CreateNode();
        var session = CreateSession(original);

        session.WorkingNode.Properties["text"] = JsonSerializer.SerializeToElement("Changed");
        session.WorkingNode.Children[0].CatalogId = "ui.changed";
        session.WorkingNode.Style.Base = session.WorkingNode.Style.Base with { Opacity = 0.5m };

        await Assert.That(original.Properties["text"].GetString()).IsEqualTo("Original");
        await Assert.That(original.Children[0].CatalogId).IsEqualTo("ui.text");
        await Assert.That(original.Style.Base.Opacity).IsEqualTo(1m);
    }

    [Test]
    public async Task Cancel_restores_the_original_snapshot()
    {
        var session = CreateSession(CreateNode());
        session.WorkingNode.Properties["text"] = JsonSerializer.SerializeToElement("Changed");

        var restored = session.Cancel();

        await Assert.That(restored.Properties["text"].GetString()).IsEqualTo("Original");
    }

    [Test]
    public async Task Apply_returns_an_isolated_copy_of_the_working_node()
    {
        var session = CreateSession(CreateNode());
        session.WorkingNode.Properties["text"] = JsonSerializer.SerializeToElement("Applied");

        var applied = session.Apply();
        session.WorkingNode.Properties["text"] = JsonSerializer.SerializeToElement("Changed again");

        await Assert.That(applied.Properties["text"].GetString()).IsEqualTo("Applied");
    }

    [Test]
    public async Task Session_carries_breakpoint_culture_and_direction()
    {
        var session = CreateSession(CreateNode());

        await Assert.That(session.Context.Breakpoint).IsEqualTo(EditorBreakpoint.Mobile);
        await Assert.That(session.Context.Culture).IsEqualTo("ar-SA");
        await Assert.That(session.Context.Direction).IsEqualTo(ContentDirection.RightToLeft);
    }

    private static NodeEditorSession CreateSession(NeoPageNode node) =>
        new(
            node,
            new NodeEditorContext(
                EditorBreakpoint.Mobile,
                "ar-SA",
                ContentDirection.RightToLeft));

    private static NeoPageNode CreateNode() =>
        new()
        {
            NodeId = "root",
            CatalogId = "ui.card",
            Kind = NeoPageNodeKind.Component,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement("Original")
            },
            Style = new ResponsiveNodeStyle
            {
                Base = new NodeStyle { Opacity = 1m }
            },
            Children =
            [
                new NeoPageNode
                {
                    NodeId = "child",
                    CatalogId = "ui.text",
                    Kind = NeoPageNodeKind.Primitive
                }
            ]
        };
}
