using System.Text.Json;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Abstractions.Tests;

public sealed class EditorBlockCompositionTests
{
    [Test]
    public async Task Clipboard_memento_and_deep_clone_create_an_independent_paste()
    {
        var original = new EditorBlock
        {
            EditorId = "original",
            Type = "ui.card",
            CompositionNodes =
            [
                new NeoPageNode
                {
                    NodeId = "root",
                    CatalogId = "ui.card",
                    Kind = NeoPageNodeKind.Component
                }
            ]
        };

        var clipboard = EditorBlockListMemento.Capture([original]);
        var pasted = clipboard.Restore().Single().CreateClipboardClone();

        await Assert.That(pasted.EditorId).IsNotEqualTo(original.EditorId);
        await Assert.That(pasted.CompositionNodes[0].NodeId)
            .IsNotEqualTo(original.CompositionNodes[0].NodeId);
        pasted.CompositionNodes[0].CatalogId = "changed";
        await Assert.That(original.CompositionNodes[0].CatalogId)
            .IsEqualTo("ui.card");
    }

    [Test]
    public async Task DeepClone_copies_composition_subtree_with_new_editor_id()
    {
        var original = new EditorBlock
        {
            Type = "primitive.text",
            CompositionNodes =
            [
                new NeoPageNode
                {
                    NodeId = "text-1",
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

        await Assert.That(clone.EditorId).IsNotEqualTo(original.EditorId);
        await Assert.That(original.CompositionNodes[0].Properties["text"].GetString())
            .IsEqualTo("Original");
        await Assert.That(clone.CompositionNodes[0].Properties["text"].GetString())
            .IsEqualTo("Changed");
    }

    [Test]
    public async Task Source_generated_json_round_trips_composition_nodes()
    {
        var original = new EditorBlock
        {
            Type = "primitive.text",
            CompositionNodes =
            [
                new NeoPageNode
                {
                    NodeId = "text-1",
                    CatalogId = "primitive.text",
                    Kind = NeoPageNodeKind.Primitive,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["text"] = JsonSerializer.SerializeToElement("Localized text")
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(original, BlockJsonContext.Default.EditorBlock);
        var restored = JsonSerializer.Deserialize(
            json,
            BlockJsonContext.Default.EditorBlock);

        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.CompositionNodes).Count().IsEqualTo(1);
        await Assert.That(restored.CompositionNodes[0].Properties["text"].GetString())
            .IsEqualTo("Localized text");
    }
}
