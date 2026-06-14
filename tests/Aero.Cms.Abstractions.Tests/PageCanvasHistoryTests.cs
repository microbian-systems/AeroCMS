using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Tests;

public sealed class PageCanvasHistoryTests
{
    [Test]
    public async Task Undo_and_redo_restore_custom_component_insertion()
    {
        var existing = Block("existing", "ui.text");
        var inserted = Block("custom", "ui.card");
        var history = new PageCanvasHistory([existing]);

        history.Record([existing, inserted]);
        var undone = history.Undo();
        var redone = history.Redo();

        await Assert.That(undone).Count().IsEqualTo(1);
        await Assert.That(redone).Count().IsEqualTo(2);
        await Assert.That(redone[1].EditorId).IsEqualTo("custom");
        await Assert.That(redone[1].CompositionNodes[0].CatalogId)
            .IsEqualTo("ui.card");
    }

    [Test]
    public async Task Restored_snapshots_preserve_ids_and_are_isolated()
    {
        var history = new PageCanvasHistory([Block("existing", "ui.text")]);
        history.Record(
        [
            Block("existing", "ui.text"),
            Block("custom", "ui.card")
        ]);

        var first = history.Undo();
        first[0].CompositionNodes[0].CatalogId = "changed";
        var second = history.Redo();
        history.Undo();
        var restored = history.Undo();

        await Assert.That(second[0].EditorId).IsEqualTo("existing");
        await Assert.That(restored[0].CompositionNodes[0].CatalogId)
            .IsEqualTo("ui.text");
    }

    [Test]
    public async Task New_canvas_mutation_after_undo_clears_redo()
    {
        var existing = Block("existing", "ui.text");
        var history = new PageCanvasHistory([existing]);
        history.Record([existing, Block("first", "ui.card")]);
        history.Undo();

        history.Record([existing, Block("replacement", "ui.container")]);

        await Assert.That(history.CanRedo).IsFalse();
    }

    [Test]
    public async Task Reorder_duplicate_and_delete_are_reversible()
    {
        var first = Block("first", "ui.text");
        var second = Block("second", "ui.card");
        var duplicate = Block("duplicate", "ui.card");
        var history = new PageCanvasHistory([first, second]);

        history.Record([second, first]);
        history.Record([second, duplicate, first]);
        history.Record([duplicate, first]);

        var beforeDelete = history.Undo();
        var beforeDuplicate = history.Undo();
        var original = history.Undo();

        await Assert.That(string.Join(",", beforeDelete.Select(block => block.EditorId)))
            .IsEqualTo("second,duplicate,first");
        await Assert.That(string.Join(",", beforeDuplicate.Select(block => block.EditorId)))
            .IsEqualTo("second,first");
        await Assert.That(string.Join(",", original.Select(block => block.EditorId)))
            .IsEqualTo("first,second");
    }

    private static EditorBlock Block(string editorId, string catalogId) =>
        new()
        {
            EditorId = editorId,
            Type = catalogId,
            CompositionNodes =
            [
                new NeoPageNode
                {
                    NodeId = $"{editorId}-root",
                    CatalogId = catalogId,
                    Kind = NeoPageNodeKind.Component
                }
            ]
        };
}
