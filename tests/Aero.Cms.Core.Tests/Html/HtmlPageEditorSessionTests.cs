using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlPageEditorSessionTests
{
    [Test]
    public async Task AddElement_SelectsNode_AndSupportsUndoRedo()
    {
        var session = CreateSession();

        var added = session.AddElement("p");

        var addedParagraph = (added as Result<HtmlNode>.Ok)?.Value;
        await Assert.That(addedParagraph).IsNotNull();
        await Assert.That(addedParagraph!.Children.Single().Text).IsEqualTo("Start writing here...");
        await Assert.That(session.SelectedNodeId).IsEqualTo(addedParagraph.NodeId);
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
        await Assert.That(session.SelectedNodeId).IsNull();
        await Assert.That(session.CanRedo).IsTrue();

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().TagName).IsEqualTo("p");
    }

    [Test]
    public async Task AddElement_UsesSelectedContainer_AndFallsBackToItsParent()
    {
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Existing copy"));
        section.Children.Add(paragraph);
        var session = CreateSession(section);

        session.Select(section.NodeId);
        var heading = session.AddElement("h2") as Result<HtmlNode>.Ok;
        await Assert.That(heading).IsNotNull();
        await Assert.That(section.Children.Last().TagName).IsEqualTo("h2");

        session.Select(paragraph.NodeId);
        var container = session.AddElement("div") as Result<HtmlNode>.Ok;
        await Assert.That(container).IsNotNull();
        await Assert.That(section.Children.Last().TagName).IsEqualTo("div");
    }

    [Test]
    public async Task AddLayout_CompilesStyles_AndRemoveRestoresEmptyCanvas()
    {
        var session = CreateSession();

        var added = session.AddLayout(HtmlLayoutStarterKind.ThreeColumns);

        await Assert.That(added).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.CompiledStyles).IsNotNull();
        await Assert.That(session.CompiledStyles!.CssText).Contains("grid-template-columns: repeat(3");
        await Assert.That(session.StyleCompilationError).IsNull();

        await Assert.That(session.RemoveSelected()).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
    }

    private static HtmlPageEditorSession CreateSession(params HtmlNode[] children)
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var content = new HtmlPageContent();
        content.Root.Children.AddRange(children);

        return new HtmlPageEditorSession(
            content,
            catalog,
            new HtmlContentModelPolicy(catalog),
            new HtmlLayoutStarterFactory(catalog),
            new NativeCssStyleCompiler(),
            new NativeStyleProfile());
    }
}
