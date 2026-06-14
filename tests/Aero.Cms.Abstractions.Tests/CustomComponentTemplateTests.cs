using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Tests;

public sealed class CustomComponentTemplateTests
{
    [Test]
    public async Task Capture_returns_an_isolated_template()
    {
        var source = CreateTree();

        var template = CustomComponentTemplate.Capture(source);
        template.Children[0].Properties["text"] =
            JsonSerializer.SerializeToElement("Changed");

        await Assert.That(source.Children[0].Properties["text"].GetString())
            .IsEqualTo("Original");
    }

    [Test]
    public async Task CreateInstance_assigns_fresh_ids_without_mutating_template()
    {
        var template = CreateTree();

        var instance = CustomComponentTemplate.CreateInstance(template);

        await Assert.That(instance.NodeId).IsNotEqualTo(template.NodeId);
        await Assert.That(instance.Children[0].NodeId)
            .IsNotEqualTo(template.Children[0].NodeId);
        await Assert.That(instance.NodeId).IsNotEqualTo(instance.Children[0].NodeId);
        await Assert.That(template.NodeId).IsEqualTo("root");
        await Assert.That(template.Children[0].NodeId).IsEqualTo("child");
    }

    [Test]
    public async Task Nested_clipboard_instances_are_independent_on_every_paste()
    {
        var clipboard = CustomComponentTemplate.Capture(CreateTree());

        var firstPaste = CustomComponentTemplate.CreateInstance(clipboard);
        var secondPaste = CustomComponentTemplate.CreateInstance(clipboard);

        await Assert.That(firstPaste.NodeId).IsNotEqualTo(secondPaste.NodeId);
        await Assert.That(firstPaste.Children[0].NodeId)
            .IsNotEqualTo(secondPaste.Children[0].NodeId);
        firstPaste.Children[0].Properties["text"] =
            JsonSerializer.SerializeToElement("Changed");
        await Assert.That(secondPaste.Children[0].Properties["text"].GetString())
            .IsEqualTo("Original");
    }

    [Test]
    public async Task Referenced_catalog_ids_are_distinct_and_sorted()
    {
        var template = CreateTree();
        template.Children.Add(new NeoPageNode
        {
            NodeId = "duplicate",
            CatalogId = "UI.TEXT",
            Kind = NeoPageNodeKind.Primitive
        });

        var catalogIds = CustomComponentTemplate.GetReferencedCatalogIds(template);

        await Assert.That(catalogIds).IsEquivalentTo(["ui.card", "ui.text"]);
        await Assert.That(catalogIds[0]).IsEqualTo("ui.card");
        await Assert.That(catalogIds[1]).IsEqualTo("ui.text");
    }

    private static NeoPageNode CreateTree() =>
        new()
        {
            NodeId = "root",
            CatalogId = "ui.card",
            Kind = NeoPageNodeKind.Component,
            Children =
            [
                new NeoPageNode
                {
                    NodeId = "child",
                    CatalogId = "ui.text",
                    Kind = NeoPageNodeKind.Primitive,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["text"] = JsonSerializer.SerializeToElement("Original")
                    }
                }
            ]
        };
}
