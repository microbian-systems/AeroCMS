using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlTreeEditorTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();

    [Test]
    public async Task Insert_undo_and_redo_mutate_only_through_editor_history()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        content.Root.Children.Add(section);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var insert = editor.InsertChild(section.NodeId, HtmlNode.CreateElement("p"));

        await Assert.That(insert).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(editor.Content.Root.Children[0].Children).Count().IsEqualTo(1);

        var undo = editor.Undo();

        await Assert.That(undo).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(editor.Content.Root.Children[0].Children).IsEmpty();

        var redo = editor.Redo();

        await Assert.That(redo).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(editor.Content.Root.Children[0].Children).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Insert_children_commits_a_fragment_as_one_atomic_undoable_change()
    {
        var content = new HtmlPageContent();
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));
        var first = HtmlNode.CreateElement("section");
        var second = HtmlNode.CreateElement("section");

        var inserted = editor.InsertChildren(content.Root.NodeId, [first, second]);

        await Assert.That(inserted).IsTypeOf<Result<IReadOnlyList<HtmlNode>>.Ok>();
        await Assert.That(editor.Content.Root.Children).Count().IsEqualTo(2);

        var undo = editor.Undo();

        await Assert.That(undo).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(editor.Content.Root.Children).IsEmpty();
    }

    [Test]
    public async Task Move_rejects_a_destination_that_violates_the_content_model()
    {
        var content = new HtmlPageContent();
        var span = HtmlNode.CreateElement("span");
        var div = HtmlNode.CreateElement("div");
        content.Root.Children.Add(span);
        content.Root.Children.Add(div);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.Move(div.NodeId, span.NodeId, 0);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(editor.Content.Root.Children).Count().IsEqualTo(2);
        await Assert.That(editor.Content.Root.Children[1]).IsSameReferenceAs(div);
    }

    [Test]
    public async Task MoveRelative_reorders_siblings_without_browser_collection_indexes()
    {
        var content = new HtmlPageContent();
        var first = HtmlNode.CreateElement("section");
        var second = HtmlNode.CreateElement("section");
        var third = HtmlNode.CreateElement("section");
        content.Root.Children.AddRange([first, second, third]);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var after = editor.MoveRelative(first.NodeId, second.NodeId, HtmlRelativePlacement.After);

        await Assert.That(after).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(content.Root.Children[0]).IsSameReferenceAs(second);
        await Assert.That(content.Root.Children[1]).IsSameReferenceAs(first);
        await Assert.That(content.Root.Children[2]).IsSameReferenceAs(third);

        var before = editor.MoveRelative(third.NodeId, second.NodeId, HtmlRelativePlacement.Before);

        await Assert.That(before).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(content.Root.Children[0]).IsSameReferenceAs(third);
        await Assert.That(content.Root.Children[1]).IsSameReferenceAs(second);
        await Assert.That(content.Root.Children[2]).IsSameReferenceAs(first);
    }

    [Test]
    public async Task MoveRelative_inside_uses_content_policy_and_one_memento()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        content.Root.Children.AddRange([section, paragraph]);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var moved = editor.MoveRelative(paragraph.NodeId, section.NodeId, HtmlRelativePlacement.Inside);

        await Assert.That(moved).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(content.Root.Children).Count().IsEqualTo(1);
        await Assert.That(section.Children.Single()).IsSameReferenceAs(paragraph);
        await Assert.That(editor.History.CanUndo).IsTrue();

        await Assert.That(editor.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(editor.Content.Root.Children).Count().IsEqualTo(2);
    }

    [Test]
    public async Task MoveRelative_rejects_invalid_nesting_without_capturing_history()
    {
        var content = new HtmlPageContent();
        var span = HtmlNode.CreateElement("span");
        var section = HtmlNode.CreateElement("section");
        content.Root.Children.AddRange([span, section]);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.MoveRelative(section.NodeId, span.NodeId, HtmlRelativePlacement.Inside);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(editor.History.CanUndo).IsFalse();
        await Assert.That(content.Root.Children).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Move_transfers_between_containers_as_one_undoable_and_redoable_change()
    {
        var content = new HtmlPageContent();
        var left = HtmlNode.CreateElement("section");
        var right = HtmlNode.CreateElement("section");
        var first = HtmlNode.CreateElement("p");
        first.Children.Add(HtmlNode.CreateText("First"));
        var moved = HtmlNode.CreateElement("p");
        moved.Children.Add(HtmlNode.CreateText("Move me"));
        var existing = HtmlNode.CreateElement("p");
        existing.Children.Add(HtmlNode.CreateText("Existing"));
        left.Children.AddRange([first, moved]);
        right.Children.Add(existing);
        content.Root.Children.AddRange([left, right]);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.Move(moved.NodeId, right.NodeId, 0);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(left.Children).Count().IsEqualTo(1);
        await Assert.That(right.Children.Select(ReadText)).IsEquivalentTo(["Move me", "Existing"]);
        await Assert.That(editor.History.CanUndo).IsTrue();

        await Assert.That(editor.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        var restoredLeft = editor.Content.Root.Children[0];
        var restoredRight = editor.Content.Root.Children[1];
        await Assert.That(restoredLeft.Children.Select(ReadText)).IsEquivalentTo(["First", "Move me"]);
        await Assert.That(restoredRight.Children.Select(ReadText)).IsEquivalentTo(["Existing"]);
        await Assert.That(editor.History.CanUndo).IsFalse();
        await Assert.That(editor.History.CanRedo).IsTrue();

        await Assert.That(editor.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(editor.Content.Root.Children[0].Children.Select(ReadText)).IsEquivalentTo(["First"]);
        await Assert.That(editor.Content.Root.Children[1].Children.Select(ReadText))
            .IsEquivalentTo(["Move me", "Existing"]);
    }

    [Test]
    public async Task Move_rejects_a_descendant_destination_without_mutating_or_capturing_history()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        var container = HtmlNode.CreateElement("div");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Nested"));
        container.Children.Add(paragraph);
        section.Children.Add(container);
        content.Root.Children.Add(section);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.Move(section.NodeId, container.NodeId, 0);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(content.Root.Children.Single()).IsSameReferenceAs(section);
        await Assert.That(section.Children.Single()).IsSameReferenceAs(container);
        await Assert.That(editor.History.CanUndo).IsFalse();
    }

    [Test]
    public async Task MoveRelative_effective_noop_does_not_capture_history()
    {
        var content = new HtmlPageContent();
        var first = HtmlNode.CreateElement("section");
        var second = HtmlNode.CreateElement("section");
        content.Root.Children.AddRange([first, second]);
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.MoveRelative(first.NodeId, second.NodeId, HtmlRelativePlacement.Before);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(content.Root.Children[0]).IsSameReferenceAs(first);
        await Assert.That(content.Root.Children[1]).IsSameReferenceAs(second);
        await Assert.That(editor.History.CanUndo).IsFalse();
    }

    [Test]
    public async Task InsertRelative_places_a_disconnected_node_beside_a_stable_target()
    {
        var content = new HtmlPageContent();
        var first = HtmlNode.CreateElement("section");
        var third = HtmlNode.CreateElement("section");
        content.Root.Children.AddRange([first, third]);
        var second = HtmlNode.CreateElement("section");
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.InsertRelative(second, third.NodeId, HtmlRelativePlacement.Before);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(content.Root.Children[0]).IsSameReferenceAs(first);
        await Assert.That(content.Root.Children[1]).IsSameReferenceAs(second);
        await Assert.That(content.Root.Children[2]).IsSameReferenceAs(third);
        await Assert.That(editor.History.CanUndo).IsTrue();
    }

    [Test]
    public async Task InsertRelative_rejects_invalid_inside_target_without_capturing_history()
    {
        var content = new HtmlPageContent();
        var paragraph = HtmlNode.CreateElement("p");
        content.Root.Children.Add(paragraph);
        var section = HtmlNode.CreateElement("section");
        var editor = new HtmlTreeEditor(content, new HtmlContentModelPolicy(Catalog));

        var result = editor.InsertRelative(section, paragraph.NodeId, HtmlRelativePlacement.Inside);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(paragraph.Children).IsEmpty();
        await Assert.That(editor.History.CanUndo).IsFalse();
    }

    private static string ReadText(HtmlNode node) => node.Children.SingleOrDefault()?.Text ?? string.Empty;
}
