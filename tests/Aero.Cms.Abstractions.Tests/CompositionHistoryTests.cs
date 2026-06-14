using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Tests;

public sealed class CompositionHistoryTests
{
    [Test]
    public async Task UndoAndRedoRestoreIsolatedSnapshots()
    {
        var initial = Node("Initial");
        var history = new CompositionHistory(initial);

        history.Record(Node("Changed"));
        var undone = history.Undo();
        undone.Properties["text"] = JsonSerializer.SerializeToElement("Mutated copy");
        var redone = history.Redo();

        await Assert.That(redone.Properties["text"].GetString()).IsEqualTo("Changed");
        await Assert.That(history.Current.Properties["text"].GetString()).IsEqualTo("Changed");
    }

    [Test]
    public async Task NewEditAfterUndoClearsRedo()
    {
        var history = new CompositionHistory(Node("Initial"));
        history.Record(Node("First"));
        history.Undo();

        history.Record(Node("Replacement"));

        await Assert.That(history.CanRedo).IsFalse();
        await Assert.That(history.Current.Properties["text"].GetString())
            .IsEqualTo("Replacement");
    }

    [Test]
    public async Task MatchingKeysCoalesceIntoOneUndoStep()
    {
        var history = new CompositionHistory(Node("Initial"));

        history.Record(Node("Typing 1"), "text:root");
        history.Record(Node("Typing 2"), "text:root");
        var undone = history.Undo();

        await Assert.That(undone.Properties["text"].GetString()).IsEqualTo("Initial");
        await Assert.That(history.CanUndo).IsFalse();
    }

    [Test]
    public async Task CapacityDropsOldestUndoEntries()
    {
        var history = new CompositionHistory(Node("Zero"), capacity: 2);
        history.Record(Node("One"));
        history.Record(Node("Two"));
        history.Record(Node("Three"));

        history.Undo();
        var oldestAvailable = history.Undo();

        await Assert.That(oldestAvailable.Properties["text"].GetString()).IsEqualTo("One");
        await Assert.That(history.CanUndo).IsFalse();
    }

    private static NeoPageNode Node(string text) =>
        new()
        {
            NodeId = "root",
            CatalogId = "primitive.text",
            Kind = NeoPageNodeKind.Primitive,
            Properties = new Dictionary<string, JsonElement>
            {
                ["text"] = JsonSerializer.SerializeToElement(text)
            }
        };
}
