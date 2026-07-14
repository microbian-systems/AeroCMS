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

    [Test]
    public async Task AddTable_creates_an_editable_semantic_two_column_table()
    {
        var session = CreateSession();

        var added = session.AddElement("table") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        var table = added!.Value;
        await Assert.That(table.Children.Select(node => node.TagName ?? string.Empty)).IsEquivalentTo(["thead", "tbody"]);
        await Assert.That(table.Children[0].Children.Single().Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["th", "th"]);
        await Assert.That(table.Children[1].Children.Single().Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["td", "td"]);
        await Assert.That(table.Children[0].Children[0].Children[0].Attributes["scope"]).IsEqualTo("col");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
    }

    [Test]
    public async Task AddForm_creates_accessible_static_controls_without_processing_behavior()
    {
        var session = CreateSession();

        var added = session.AddElement("form") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        var form = added!.Value;
        await Assert.That(form.Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["label", "input", "button"]);
        var label = form.Children[0];
        var input = form.Children[1];
        await Assert.That(label.Attributes["for"]).IsEqualTo(input.Attributes["id"]);
        await Assert.That(input.Attributes["type"]).IsEqualTo("text");
        await Assert.That(form.Children[2].Attributes["type"]).IsEqualTo("submit");
    }

    [Test]
    public async Task UpdateSelectedProperties_replaces_textarea_literal_content_atomically()
    {
        var textArea = HtmlNode.CreateElement("textarea");
        textArea.Children.Add(HtmlNode.CreateText("Before"));
        var session = CreateSession(textArea);
        session.Select(textArea.NodeId);
        var properties = HtmlNodeProperties.From(textArea);
        properties.Attributes["name"] = "message";
        properties.ReplaceChildrenWithLiteralText = true;
        properties.LiteralText = "After";

        var updated = session.UpdateSelectedProperties(properties);

        await Assert.That(updated).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.SelectedNode!.Attributes["name"]).IsEqualTo("message");
        await Assert.That(session.SelectedNode.Children.Single().Text).IsEqualTo("After");
        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.SelectedNode!.Children.Single().Text).IsEqualTo("Before");
    }

    [Test]
    public async Task Move_rejects_indirectly_nested_form_without_mutating_or_capturing_history()
    {
        var outerForm = HtmlNode.CreateElement("form");
        var container = HtmlNode.CreateElement("div");
        outerForm.Children.Add(container);
        var innerForm = HtmlNode.CreateElement("form");
        var session = CreateSession(outerForm, innerForm);

        var moved = session.MoveRelative(innerForm.NodeId, container.NodeId, HtmlRelativePlacement.Inside);

        await Assert.That(moved).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.Content.Root.Children).Count().IsEqualTo(2);
        await Assert.That(container.Children).IsEmpty();
        await Assert.That(session.CanUndo).IsFalse();
    }

    [Test]
    public async Task UpdateSelectedProperties_CommitsValidatedCandidate_AndSupportsUndoRedo()
    {
        var section = HtmlNode.CreateElement("section");
        var session = CreateSession(section);
        session.Select(section.NodeId);

        var properties = HtmlNodeProperties.From(section);
        properties.Attributes["id"] = "welcome";
        properties.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1.5m),
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Hex("#f8fafc")
            }
        };

        var updated = session.UpdateSelectedProperties(properties);

        await Assert.That(updated).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.SelectedNode!.Attributes["id"]).IsEqualTo("welcome");
        await Assert.That(session.SelectedNode.Style!.GridColumns).IsEqualTo(2);
        await Assert.That(session.CompiledStyles!.CssText).Contains("grid-template-columns: repeat(2");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.SelectedNode!.Attributes.ContainsKey("id")).IsFalse();
        await Assert.That(session.SelectedNode.Style).IsNull();

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.SelectedNode!.Attributes["id"]).IsEqualTo("welcome");
    }

    [Test]
    public async Task UpdateSelectedProperties_RejectsUnsafeCandidate_WithoutMutatingOrCapturingHistory()
    {
        var link = HtmlNode.CreateElement("a");
        link.Attributes["href"] = "/safe";
        link.Children.Add(HtmlNode.CreateText("Safe link"));
        var session = CreateSession(link);
        session.Select(link.NodeId);

        var properties = HtmlNodeProperties.From(link);
        properties.Attributes["href"] = "javascript:alert(1)";

        var updated = session.UpdateSelectedProperties(properties);

        await Assert.That(updated).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.SelectedNode!.Attributes["href"]).IsEqualTo("/safe");
        await Assert.That(session.CanUndo).IsFalse();
    }

    [Test]
    public async Task UpdateSelectedChildren_ValidatesAndCommitsOneUndoableChange()
    {
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Before"));
        var session = CreateSession(paragraph);
        session.Select(paragraph.NodeId);

        var strong = HtmlNode.CreateElement("strong");
        strong.Children.Add(HtmlNode.CreateText("After"));
        var result = session.UpdateSelectedChildren([strong]);

        await Assert.That(result).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.SelectedNode!.Children.Single().TagName).IsEqualTo("strong");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.SelectedNode!.Children.Single().Text).IsEqualTo("Before");
    }

    [Test]
    public async Task RichTextUpdate_RoundTripsSupportedMarks_AsOneUndoableChange()
    {
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Before"));
        var session = CreateSession(paragraph);
        session.Select(paragraph.NodeId);
        const string documentJson = """
            {
              "type": "doc",
              "content": [{
                "type": "paragraph",
                "content": [
                  { "type": "text", "text": "Bold emphasis", "marks": [
                    { "type": "bold" }, { "type": "italic" }
                  ]},
                  { "type": "text", "text": " and " },
                  { "type": "text", "text": "documentation", "marks": [{
                    "type": "link",
                    "attrs": { "href": "/docs", "target": "_blank", "rel": "noopener" }
                  }]}
                ]
              }]
            }
            """;
        var converter = new TiptapInlineContentConverter();
        var converted = converter.FromDocumentJson(documentJson) as Result<IReadOnlyList<HtmlNode>>.Ok;

        await Assert.That(converted).IsNotNull();
        var updated = session.UpdateSelectedChildren(converted!.Value);

        await Assert.That(updated).IsTypeOf<Result<HtmlNode>.Ok>();
        var editorHtml = converter.ToEditorHtml(session.SelectedNode!) as Result<string>.Ok;
        await Assert.That(editorHtml).IsNotNull();
        await Assert.That(editorHtml!.Value)
            .IsEqualTo("<p><strong><em>Bold emphasis</em></strong> and <a href=\"/docs\" rel=\"noopener\" target=\"_blank\">documentation</a></p>");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.SelectedNode!.Children.Single().Text).IsEqualTo("Before");
    }

    [Test]
    public async Task RichTextUpdate_RejectsUnsafeLink_WithoutMutatingContentOrHistory()
    {
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Safe content"));
        var session = CreateSession(paragraph);
        session.Select(paragraph.NodeId);
        const string documentJson = """
            { "type": "doc", "content": [{ "type": "paragraph", "content": [{
              "type": "text",
              "text": "Unsafe",
              "marks": [{ "type": "link", "attrs": { "href": "javascript:alert(1)" } }]
            }]}]}
            """;
        var converted = new TiptapInlineContentConverter().FromDocumentJson(documentJson)
            as Result<IReadOnlyList<HtmlNode>>.Ok;

        await Assert.That(converted).IsNotNull();
        var updated = session.UpdateSelectedChildren(converted!.Value);

        await Assert.That(updated).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.SelectedNode!.Children.Single().Text).IsEqualTo("Safe content");
        await Assert.That(session.CanUndo).IsFalse();
    }

    [Test]
    public async Task MoveRelative_selects_the_moved_node_and_supports_undo()
    {
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Move me"));
        var session = CreateSession(section, paragraph);

        var moved = session.MoveRelative(
            paragraph.NodeId,
            section.NodeId,
            HtmlRelativePlacement.Inside);

        await Assert.That(moved).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.SelectedNodeId).IsEqualTo(paragraph.NodeId);
        await Assert.That(section.Children.Single()).IsSameReferenceAs(paragraph);

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).Count().IsEqualTo(2);
    }

    [Test]
    public async Task AddElementRelative_uses_palette_defaults_and_selects_the_inserted_node()
    {
        var section = HtmlNode.CreateElement("section");
        var session = CreateSession(section);

        var added = session.AddElementRelative(
            "p",
            section.NodeId,
            HtmlRelativePlacement.Inside);

        var paragraph = (added as Result<HtmlNode>.Ok)?.Value;
        await Assert.That(paragraph).IsNotNull();
        await Assert.That(paragraph!.Children.Single().Text).IsEqualTo("Start writing here...");
        await Assert.That(section.Children.Single()).IsSameReferenceAs(paragraph);
        await Assert.That(session.SelectedNodeId).IsEqualTo(paragraph.NodeId);
    }

    [Test]
    public async Task AddLayoutRelative_inserts_the_complete_layout_as_one_undoable_change()
    {
        var section = HtmlNode.CreateElement("section");
        var session = CreateSession(section);

        var added = session.AddLayoutRelative(
            HtmlLayoutStarterKind.TwoColumns,
            section.NodeId,
            HtmlRelativePlacement.After);

        var layout = (added as Result<HtmlNode>.Ok)?.Value;
        await Assert.That(layout).IsNotNull();
        await Assert.That(session.Content.Root.Children).Count().IsEqualTo(2);
        await Assert.That(layout!.Children.Single().Children).Count().IsEqualTo(2);
        await Assert.That(session.CanUndo).IsTrue();
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
            new HtmlContentValidator(
                catalog,
                new HtmlContentModelPolicy(catalog),
                new HtmlAttributePolicy()),
            new HtmlLayoutStarterFactory(catalog),
            new NativeCssStyleCompiler(),
            new NativeStyleProfile());
    }
}
