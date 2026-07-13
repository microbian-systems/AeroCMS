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
}
