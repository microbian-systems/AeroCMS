using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;
using Aero.Core.Railway;
using System.Text.Json;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlPageEditorSessionTests
{
    [Test]
    public async Task Registered_fragment_parameters_share_history_duplication_and_orphan_reconciliation()
    {
        var session = CreateSession();
        var added = session.AddRegisteredFragment(
            "CORE.SITE-NOTICE",
            new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("Initial")
            });
        var node = ((Result<HtmlNode>.Ok)added).Value;
        await Assert.That(session.Composition.RegisteredFragments.Single().Key)
            .IsEqualTo("core.site-notice");

        var updated = session.UpdateRegisteredFragmentParameters(
            node.NodeId,
            new Dictionary<string, JsonElement>
            {
                ["message"] = JsonSerializer.SerializeToElement("Updated")
            });
        await Assert.That(updated).IsTypeOf<Result<PageRegisteredFragment>.Ok>();
        await Assert.That(session.Composition.RegisteredFragments.Single().Parameters["message"].GetString())
            .IsEqualTo("Updated");

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.RegisteredFragments.Single().Parameters["message"].GetString())
            .IsEqualTo("Initial");
        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();

        session.Select(node.NodeId);
        await Assert.That(session.DuplicateSelected()).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Composition.RegisteredFragments).Count().IsEqualTo(2);
        await Assert.That(session.Composition.RegisteredFragments.All(fragment =>
            fragment.Parameters["message"].GetString() == "Updated")).IsTrue();

        await Assert.That(session.RemoveSelected()).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Composition.RegisteredFragments).Count().IsEqualTo(1);
        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.RegisteredFragments).Count().IsEqualTo(2);
    }

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
    public async Task DuplicateSelected_creates_a_fresh_sibling_as_one_undoable_change()
    {
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Duplicate me"));
        section.Children.Add(paragraph);
        var session = CreateSession(section);
        session.Select(paragraph.NodeId);

        var duplicated = session.DuplicateSelected() as Result<HtmlNode>.Ok;

        await Assert.That(duplicated).IsNotNull();
        await Assert.That(section.Children).Count().IsEqualTo(2);
        await Assert.That(duplicated!.Value.NodeId).IsNotEqualTo(paragraph.NodeId);
        await Assert.That(duplicated.Value.Children.Single().NodeId)
            .IsNotEqualTo(paragraph.Children.Single().NodeId);
        await Assert.That(duplicated.Value.Children.Single().Text).IsEqualTo("Duplicate me");
        await Assert.That(session.SelectedNodeId).IsEqualTo(duplicated.Value.NodeId);
        await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(session.Content.Root)).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().Children).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RemoveSelected_selects_the_next_sibling_to_preserve_keyboard_context()
    {
        var section = HtmlNode.CreateElement("section");
        var first = HtmlNode.CreateElement("p");
        var selected = HtmlNode.CreateElement("p");
        var next = HtmlNode.CreateElement("p");
        section.Children.Add(first);
        section.Children.Add(selected);
        section.Children.Add(next);
        var session = CreateSession(section);
        session.Select(selected.NodeId);

        var removed = session.RemoveSelected();

        await Assert.That(removed).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(section.Children).IsEquivalentTo([first, next]);
        await Assert.That(session.SelectedNodeId).IsEqualTo(next.NodeId);
    }

    [Test]
    public async Task RemoveSelected_selects_the_parent_when_its_last_child_is_removed()
    {
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        section.Children.Add(paragraph);
        var session = CreateSession(section);
        session.Select(paragraph.NodeId);

        var removed = session.RemoveSelected();

        await Assert.That(removed).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(section.Children).IsEmpty();
        await Assert.That(session.SelectedNodeId).IsEqualTo(section.NodeId);
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
    public async Task AddComponent_inserts_one_editable_undoable_subtree()
    {
        var session = CreateSession();

        var added = session.AddComponent("basic.hero") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        await Assert.That(added!.Value.TagName).IsEqualTo("section");
        await Assert.That(session.SelectedNodeId).IsEqualTo(added.Value.NodeId);
        await Assert.That(session.CompiledStyles!.CssText).Contains("min-height: 65vh");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().TagName).IsEqualTo("section");
    }

    [Test]
    public async Task AddPatternComponent_inserts_an_editable_undoable_composition_by_stable_key()
    {
        var session = CreateSession();

        var added = session.AddComponent("pattern.product-card") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        await Assert.That(added!.Value.TagName).IsEqualTo("article");
        await Assert.That(added.Value.ThemeClasses).Contains("d-card");
        await Assert.That(session.SelectedNodeId).IsEqualTo(added.Value.NodeId);
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().TagName).IsEqualTo("article");
    }

    [Test]
    public async Task AddTable_creates_an_editable_semantic_two_column_table()
    {
        var session = CreateSession();

        var added = session.AddElement("table") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        var table = added!.Value;
        await Assert.That(table.Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["caption", "colgroup", "thead", "tbody", "tfoot"]);
        await Assert.That(table.Children[1].Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["col", "col"]);
        await Assert.That(table.Children[2].Children.Single().Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["th", "th"]);
        await Assert.That(table.Children[3].Children.Single().Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["td", "td"]);
        await Assert.That(table.Children[4].Children.Single().Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["td", "td"]);
        await Assert.That(table.Children[2].Children[0].Children[0].Attributes["scope"]).IsEqualTo("col");
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
    public async Task AddAdvancedTableAndFormPrimitives_creates_valid_useful_defaults()
    {
        var session = CreateSession();
        var rootNodeId = session.Content.Root.NodeId;

        var fieldset = session.AddElement("fieldset", rootNodeId) as Result<HtmlNode>.Ok;
        var dataList = session.AddElement("datalist", rootNodeId) as Result<HtmlNode>.Ok;
        var output = session.AddElement("output", rootNodeId) as Result<HtmlNode>.Ok;

        await Assert.That(fieldset).IsNotNull();
        await Assert.That(fieldset!.Value.Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["legend", "label", "input"]);
        await Assert.That(fieldset.Value.Children[1].Attributes["for"])
            .IsEqualTo(fieldset.Value.Children[2].Attributes["id"]);
        await Assert.That(dataList).IsNotNull();
        await Assert.That(dataList!.Value.Children).Count().IsEqualTo(2);
        await Assert.That(dataList.Value.Children.All(node => node.TagName == "option")).IsTrue();
        await Assert.That(output).IsNotNull();
        await Assert.That(output!.Value.Attributes["name"]).IsEqualTo("result");
        await Assert.That(output.Value.Children.Single().Text).IsEqualTo("Calculated result");
        await Assert.That(session.StyleCompilationError).IsNull();
    }

    [Test]
    public async Task AddSemanticPrimitives_creates_useful_valid_default_subtrees()
    {
        var session = CreateSession();

        var descriptionList = session.AddElement("dl") as Result<HtmlNode>.Ok;
        var disclosure = session.AddElement("details") as Result<HtmlNode>.Ok;
        var blockQuote = session.AddElement("blockquote") as Result<HtmlNode>.Ok;

        await Assert.That(descriptionList).IsNotNull();
        await Assert.That(descriptionList!.Value.Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["dt", "dd"]);
        await Assert.That(disclosure).IsNotNull();
        await Assert.That(disclosure!.Value.Children[0].TagName).IsEqualTo("summary");
        await Assert.That(disclosure.Value.Children[1].TagName).IsEqualTo("p");
        await Assert.That(blockQuote).IsNotNull();
        await Assert.That(blockQuote!.Value.Children.Single().TagName).IsEqualTo("p");
        await Assert.That(session.CanUndo).IsTrue();

        var dialogSession = CreateSession();
        var dialog = dialogSession.AddElement("dialog") as Result<HtmlNode>.Ok;
        await Assert.That(dialog).IsNotNull();
        await Assert.That(dialog!.Value.Attributes.ContainsKey("open")).IsTrue();
        await Assert.That(dialog.Value.Children.Select(node => node.TagName!))
            .IsEquivalentTo(["h2", "p"]);
        await Assert.That(dialogSession.CanUndo).IsTrue();
    }

    [Test]
    public async Task AddMediaPrimitives_creates_safe_editable_default_sources()
    {
        var session = CreateSession();

        var picture = session.AddElement("picture") as Result<HtmlNode>.Ok;
        var audio = session.AddElement("audio") as Result<HtmlNode>.Ok;
        var video = session.AddElement("video") as Result<HtmlNode>.Ok;

        await Assert.That(picture).IsNotNull();
        await Assert.That(picture!.Value.Children.Select(node => node.TagName ?? string.Empty))
            .IsEquivalentTo(["source", "img"]);
        await Assert.That(picture.Value.Children[0].Attributes["srcset"]).Contains("1280w");
        await Assert.That(picture.Value.Children[1].Attributes["alt"]).IsNotEmpty();

        await Assert.That(audio).IsNotNull();
        await Assert.That(audio!.Value.Attributes.ContainsKey("controls")).IsTrue();
        await Assert.That(audio.Value.Children.Single().Attributes["type"]).IsEqualTo("audio/mpeg");

        await Assert.That(video).IsNotNull();
        await Assert.That(video!.Value.Attributes["preload"]).IsEqualTo("metadata");
        await Assert.That(video.Value.Children.Single().Attributes["type"]).IsEqualTo("video/mp4");
        await Assert.That(session.StyleCompilationError).IsNull();
    }

    [Test]
    public async Task Guided_list_item_is_valid_selected_and_one_undoable_change()
    {
        var list = HtmlNode.CreateElement("ul");
        var existing = HtmlNode.CreateElement("li");
        existing.Children.Add(HtmlNode.CreateText("Existing item"));
        list.Children.Add(existing);
        var session = CreateSession(list);
        session.Select(list.NodeId);

        var added = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddListItem)
            as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        await Assert.That(list.Children).Count().IsEqualTo(2);
        await Assert.That(added!.Value.TagName).IsEqualTo("li");
        await Assert.That(added.Value.Children.Single().Text).IsEqualTo("List item");
        await Assert.That(session.SelectedNodeId).IsEqualTo(added.Value.NodeId);
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().Children).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Guided_table_row_and_column_preserve_semantics_and_each_use_one_memento()
    {
        var session = CreateSession();
        var table = (session.AddElement("table") as Result<HtmlNode>.Ok)!.Value;
        session.Select(table.NodeId);

        var row = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddTableRow)
            as Result<HtmlNode>.Ok;

        await Assert.That(row).IsNotNull();
        await Assert.That(row!.Value.Children.Select(cell => cell.TagName ?? string.Empty))
            .IsEquivalentTo(["td", "td"]);

        session.Select(table.NodeId);
        var column = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddTableColumn)
            as Result<HtmlNode>.Ok;

        await Assert.That(column).IsNotNull();
        var currentTable = session.SelectedNode!;
        var rows = Flatten(currentTable).Where(node => node.TagName == "tr").ToArray();
        await Assert.That(rows).Count().IsEqualTo(4);
        await Assert.That(rows.All(item => item.Children.Count == 3)).IsTrue();
        await Assert.That(rows[0].Children.All(cell => cell.TagName == "th")).IsTrue();
        await Assert.That(rows.Skip(1).SelectMany(item => item.Children).All(cell => cell.TagName == "td")).IsTrue();
        await Assert.That(currentTable.Children.Single(node => node.TagName == "colgroup").Children)
            .Count().IsEqualTo(3);

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(Flatten(session.SelectedNode!).Where(node => node.TagName == "tr")
            .All(item => item.Children.Count == 2)).IsTrue();
    }

    [Test]
    public async Task Guided_media_actions_keep_sources_before_fallback_image_and_add_caption_track()
    {
        var session = CreateSession();
        var picture = (session.AddElement("picture") as Result<HtmlNode>.Ok)!.Value;
        session.Select(picture.NodeId);

        var pictureSource = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddMediaSource)
            as Result<HtmlNode>.Ok;

        await Assert.That(pictureSource).IsNotNull();
        var currentPicture = session.Content.Root.Children.Single(node => node.TagName == "picture");
        await Assert.That(currentPicture.Children.Last().TagName).IsEqualTo("img");
        await Assert.That(currentPicture.Children.Count(node => node.TagName == "source")).IsEqualTo(2);

        var video = (session.AddElement("video", session.Content.Root.NodeId) as Result<HtmlNode>.Ok)!.Value;
        session.Select(video.NodeId);
        var track = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddMediaTrack)
            as Result<HtmlNode>.Ok;

        await Assert.That(track).IsNotNull();
        await Assert.That(track!.Value.Attributes["kind"]).IsEqualTo("captions");
        await Assert.That(track.Value.Attributes["srclang"]).IsEqualTo("en");
        await Assert.That(video.Children.Last().TagName).IsEqualTo("track");
        await Assert.That(session.StyleCompilationError).IsNull();
    }

    [Test]
    public async Task Guided_form_actions_create_accessible_fields_and_editable_options()
    {
        var session = CreateSession();
        var form = (session.AddElement("form") as Result<HtmlNode>.Ok)!.Value;
        session.Select(form.NodeId);

        var input = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddFormInput)
            as Result<HtmlNode>.Ok;
        session.Select(form.NodeId);
        var textArea = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddFormTextArea)
            as Result<HtmlNode>.Ok;
        session.Select(form.NodeId);
        var select = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddFormSelect)
            as Result<HtmlNode>.Ok;

        await Assert.That(input).IsNotNull();
        await Assert.That(textArea).IsNotNull();
        await Assert.That(select).IsNotNull();
        await Assert.That(input!.Value.Attributes["type"]).IsEqualTo("text");
        await Assert.That(textArea!.Value.Attributes["rows"]).IsEqualTo("5");
        await Assert.That(select!.Value.Children.Single().TagName).IsEqualTo("option");

        var fields = form.Children.Where(child => child.TagName == "div").ToArray();
        await Assert.That(fields).Count().IsEqualTo(3);
        foreach (var field in fields)
        {
            var label = field.Children.Single(child => child.TagName == "label");
            var control = field.Children.Single(child => child.TagName is "input" or "textarea" or "select");
            await Assert.That(label.Attributes["for"]).IsEqualTo(control.Attributes["id"]);
        }

        session.Select(select.Value.NodeId);
        var option = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddSelectOption)
            as Result<HtmlNode>.Ok;

        await Assert.That(option).IsNotNull();
        await Assert.That(select.Value.Children).Count().IsEqualTo(2);
        await Assert.That(option!.Value.Attributes["value"]).IsEqualTo("option-2");
        await Assert.That(session.CanUndo).IsTrue();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        var restoredSelect = HtmlTreeOperations.FindById(session.Content.Root, select.Value.NodeId)!;
        await Assert.That(restoredSelect.Children).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Guided_option_actions_support_groups_and_suggested_values()
    {
        var session = CreateSession();
        var select = (session.AddElement("select") as Result<HtmlNode>.Ok)!.Value;
        session.Select(select.NodeId);

        var group = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddOptionGroup)
            as Result<HtmlNode>.Ok;

        await Assert.That(group).IsNotNull();
        await Assert.That(group!.Value.TagName).IsEqualTo("optgroup");
        await Assert.That(group.Value.Attributes["label"]).IsEqualTo("Option group 1");
        await Assert.That(group.Value.Children.Single().TagName).IsEqualTo("option");

        session.Select(group.Value.NodeId);
        var groupedOption = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddSelectOption)
            as Result<HtmlNode>.Ok;
        await Assert.That(groupedOption).IsNotNull();
        await Assert.That(group.Value.Children).Count().IsEqualTo(2);

        var dataList = (session.AddElement("datalist", session.Content.Root.NodeId) as Result<HtmlNode>.Ok)!.Value;
        session.Select(dataList.NodeId);
        var suggestedOption = session.ApplySelectedCollectionAction(HtmlCollectionActionKind.AddSelectOption)
            as Result<HtmlNode>.Ok;
        await Assert.That(suggestedOption).IsNotNull();
        await Assert.That(dataList.Children).Count().IsEqualTo(3);
        await Assert.That(session.StyleCompilationError).IsNull();
    }

    [Test]
    public async Task AddLowRiskSemanticPrimitives_creates_valid_useful_defaults()
    {
        var session = CreateSession();
        var rootNodeId = session.Content.Root.NodeId;

        var address = session.AddElement("address", rootNodeId) as Result<HtmlNode>.Ok;
        var time = session.AddElement("time", rootNodeId) as Result<HtmlNode>.Ok;
        var data = session.AddElement("data", rootNodeId) as Result<HtmlNode>.Ok;
        var progress = session.AddElement("progress", rootNodeId) as Result<HtmlNode>.Ok;
        var meter = session.AddElement("meter", rootNodeId) as Result<HtmlNode>.Ok;

        await Assert.That(address).IsNotNull();
        await Assert.That(address!.Value.Children.Single().TagName).IsEqualTo("p");
        await Assert.That(time).IsNotNull();
        await Assert.That(time!.Value.Attributes["datetime"]).IsEqualTo("2026-01-01");
        await Assert.That(data).IsNotNull();
        await Assert.That(data!.Value.Attributes["value"]).IsEqualTo("42");
        await Assert.That(progress).IsNotNull();
        await Assert.That(progress!.Value.Attributes["max"]).IsEqualTo("1");
        await Assert.That(meter).IsNotNull();
        await Assert.That(meter!.Value.Attributes["low"]).IsEqualTo("0.3");
        await Assert.That(session.StyleCompilationError).IsNull();
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
                  { "type": "text", "text": "obsolete", "marks": [{ "type": "strike" }] },
                  { "type": "text", "text": " " },
                  { "type": "text", "text": "OldApi()", "marks": [{ "type": "code" }] },
                  { "type": "text", "text": " " },
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
            .IsEqualTo("<p><strong><em>Bold emphasis</em></strong> and <s>obsolete</s> <code>OldApi()</code> <a href=\"/docs\" rel=\"noopener\" target=\"_blank\">documentation</a></p>");
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
    public async Task MoveSelectedSibling_reorders_the_selected_node_and_updates_available_actions()
    {
        var first = HtmlNode.CreateElement("p");
        var second = HtmlNode.CreateElement("p");
        var third = HtmlNode.CreateElement("p");
        var session = CreateSession(first, second, third);
        session.Select(second.NodeId);

        await Assert.That(session.CanMoveSelectedUp).IsTrue();
        await Assert.That(session.CanMoveSelectedDown).IsTrue();

        var movedUp = session.MoveSelectedUp();

        await Assert.That(movedUp).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Content.Root.Children[0].NodeId).IsEqualTo(second.NodeId);
        await Assert.That(session.Content.Root.Children[1].NodeId).IsEqualTo(first.NodeId);
        await Assert.That(session.Content.Root.Children[2].NodeId).IsEqualTo(third.NodeId);
        await Assert.That(session.CanMoveSelectedUp).IsFalse();
        await Assert.That(session.CanMoveSelectedDown).IsTrue();

        var movedDown = session.MoveSelectedDown();

        await Assert.That(movedDown).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Content.Root.Children[0].NodeId).IsEqualTo(first.NodeId);
        await Assert.That(session.Content.Root.Children[1].NodeId).IsEqualTo(second.NodeId);
        await Assert.That(session.Content.Root.Children[2].NodeId).IsEqualTo(third.NodeId);
        await Assert.That(session.CanUndo).IsTrue();
    }

    [Test]
    public async Task MoveSelectedSibling_rejects_container_boundaries_without_history()
    {
        var first = HtmlNode.CreateElement("p");
        var second = HtmlNode.CreateElement("p");
        var session = CreateSession(first, second);
        session.Select(first.NodeId);

        var beforeFirst = session.MoveSelectedUp();

        await Assert.That(beforeFirst).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.CanUndo).IsFalse();

        session.Select(second.NodeId);
        var afterLast = session.MoveSelectedDown();

        await Assert.That(afterLast).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.CanUndo).IsFalse();
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

    [Test]
    public async Task AddComponentRelative_inserts_the_complete_component_at_the_drop_location()
    {
        var section = HtmlNode.CreateElement("section");
        var session = CreateSession(section);

        var added = session.AddComponentRelative(
            "basic.call-to-action",
            section.NodeId,
            HtmlRelativePlacement.After) as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        await Assert.That(session.Content.Root.Children).Count().IsEqualTo(2);
        await Assert.That(session.Content.Root.Children[1]).IsSameReferenceAs(added!.Value);
        await Assert.That(session.SelectedNodeId).IsEqualTo(added.Value.NodeId);
        await Assert.That(session.CanUndo).IsTrue();
    }

    [Test]
    public async Task RemoveSelected_prunes_orphaned_scope_and_binding_with_aggregate_undo_redo()
    {
        var scope = HtmlNode.CreateElement("section");
        var template = HtmlNode.CreateElement("article");
        var heading = HtmlNode.CreateElement("h2");
        template.Children.Add(heading);
        scope.Children.Add(template);

        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    TemplateRootNodeId = template.NodeId,
                    ContentTypeId = 42,
                    ContentTypeAlias = "article"
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = heading.NodeId,
                    ScopeNodeId = scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };
        var session = CreateSession(composition, scope);
        session.Select(scope.NodeId);

        var removed = session.RemoveSelected();

        await Assert.That(removed).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
        await Assert.That(session.Composition.ContentLists).IsEmpty();
        await Assert.That(session.Composition.FieldBindings).IsEmpty();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(HtmlTreeOperations.FindById(session.Content.Root, scope.NodeId)).IsNotNull();
        await Assert.That(session.Composition.ContentLists).Count().IsEqualTo(1);
        await Assert.That(session.Composition.FieldBindings).Count().IsEqualTo(1);

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
        await Assert.That(session.Composition.ContentLists).IsEmpty();
        await Assert.That(session.Composition.FieldBindings).IsEmpty();
    }

    [Test]
    public async Task RemoveSelected_prunes_rendered_fragment_with_aggregate_undo_redo()
    {
        var container = HtmlNode.CreateElement("section");
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = container.NodeId,
                    Kind = PageRenderedFragmentKind.Markdown,
                    Source = "# Reversible"
                }
            ]
        };
        var session = CreateSession(composition, container);
        session.Select(container.NodeId);

        await Assert.That(session.RemoveSelected()).IsTypeOf<Result<HtmlNode>.Ok>();
        await Assert.That(session.Composition.RenderedFragments).IsEmpty();

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.RenderedFragments.Single().Source).IsEqualTo("# Reversible");

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.RenderedFragments).IsEmpty();
    }

    [Test]
    public async Task Loaded_composition_is_snapshotted_without_silent_cleanup()
    {
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = 987654321,
                    ContentTypeId = 42,
                    ContentTypeAlias = "article",
                    ContentItemId = 73
                }
            ]
        };

        var session = CreateSession(composition);

        await Assert.That(session.Composition.ContentItems).Count().IsEqualTo(1);
        await Assert.That(session.Composition.ContentItems.Single().NodeId).IsEqualTo(987654321);
        await Assert.That(session.CanUndo).IsFalse();
    }

    [Test]
    public async Task ReplaceComposition_is_undoable_without_changing_the_html_tree()
    {
        var scope = HtmlNode.CreateElement("section");
        var template = HtmlNode.CreateElement("article");
        scope.Children.Add(template);
        var session = CreateSession(scope);
        var rootNodeId = session.Content.Root.NodeId;

        var replaced = session.ReplaceComposition(new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    TemplateRootNodeId = template.NodeId,
                    ContentTypeId = 42,
                    ContentTypeAlias = "article"
                }
            ]
        });

        await Assert.That(replaced).IsTypeOf<Result<PageCompositionDocument>.Ok>();
        await Assert.That(session.Composition.ContentLists).Count().IsEqualTo(1);
        await Assert.That(session.Content.Root.NodeId).IsEqualTo(rootNodeId);

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.ContentLists).IsEmpty();
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scope.NodeId);

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.ContentLists).Count().IsEqualTo(1);
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scope.NodeId);
    }

    [Test]
    public async Task AddContentList_creates_pageable_scope_and_undoes_html_with_composition()
    {
        var session = CreateSession();

        var added = session.AddContentList(42, "article") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        await Assert.That(added!.Value.TagName).IsEqualTo("section");
        var scope = session.Composition.ContentLists.Single();
        await Assert.That(scope.NodeId).IsEqualTo(added.Value.NodeId);
        await Assert.That(scope.ContentTypeId).IsEqualTo(42);
        await Assert.That(scope.ContentTypeAlias).IsEqualTo("article");
        await Assert.That(scope.Query.PageSize).IsEqualTo(10);
        await Assert.That(HtmlTreeOperations.FindById(added.Value, scope.TemplateRootNodeId)?.TagName)
            .IsEqualTo("article");

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children).IsEmpty();
        await Assert.That(session.Composition.ContentLists).IsEmpty();

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scope.NodeId);
        await Assert.That(session.Composition.ContentLists.Single().NodeId).IsEqualTo(scope.NodeId);
    }

    [Test]
    public async Task Add_update_and_duplicate_rendered_fragment_share_aggregate_history()
    {
        var session = CreateSession();

        var added = session.AddRenderedFragment(
            PageRenderedFragmentKind.Markdown,
            "# Initial") as Result<HtmlNode>.Ok;

        await Assert.That(added).IsNotNull();
        var fragment = session.Composition.RenderedFragments.Single();
        await Assert.That(fragment.NodeId).IsEqualTo(added!.Value.NodeId);
        await Assert.That(fragment.Source).IsEqualTo("# Initial");

        var updated = session.UpdateRenderedFragmentSource(fragment.NodeId, "## Updated");
        await Assert.That(updated).IsTypeOf<Result<PageRenderedFragment>.Ok>();
        await Assert.That(session.Composition.RenderedFragments.Single().Source).IsEqualTo("## Updated");

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.RenderedFragments.Single().Source).IsEqualTo("# Initial");
        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();

        session.Select(fragment.NodeId);
        var duplicate = session.DuplicateSelected() as Result<HtmlNode>.Ok;
        await Assert.That(duplicate).IsNotNull();
        await Assert.That(session.Composition.RenderedFragments).Count().IsEqualTo(2);
        await Assert.That(session.Composition.RenderedFragments.Any(candidate =>
            candidate.NodeId == duplicate!.Value.NodeId
            && candidate.Source == "## Updated")).IsTrue();
    }

    [Test]
    public async Task AddContentItem_persists_stable_identity_with_slug_metadata()
    {
        var session = CreateSession();

        var added = session.AddContentItem(
            42,
            "article",
            73,
            "hello-world",
            "Hello world");

        await Assert.That(added).IsTypeOf<Result<HtmlNode>.Ok>();
        var scope = session.Composition.ContentItems.Single();
        await Assert.That(scope.ContentTypeId).IsEqualTo(42);
        await Assert.That(scope.LookupMode).IsEqualTo(PageContentItemLookupMode.StableId);
        await Assert.That(scope.ContentItemId).IsEqualTo(73);
        await Assert.That(scope.Slug).IsEqualTo("hello-world");
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scope.NodeId);
    }

    [Test]
    public async Task AddContentField_inside_matching_list_template_creates_atomic_binding()
    {
        var session = CreateSession();
        var addedList = session.AddContentList(42, "article") as Result<HtmlNode>.Ok;
        var listScope = session.Composition.ContentLists.Single();

        var addedField = session.AddContentField(42, "link", "url", "Read more") as Result<HtmlNode>.Ok;

        await Assert.That(addedList).IsNotNull();
        await Assert.That(addedField).IsNotNull();
        await Assert.That(addedField!.Value.TagName).IsEqualTo("a");
        var binding = session.Composition.FieldBindings.Single();
        await Assert.That(binding.NodeId).IsEqualTo(addedField.Value.NodeId);
        await Assert.That(binding.ScopeNodeId).IsEqualTo(listScope.NodeId);
        await Assert.That(binding.FieldName).IsEqualTo("link");
        await Assert.That(binding.Target).IsEqualTo(PageFieldBindingTarget.Hyperlink);

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.ContentLists).Count().IsEqualTo(1);
        await Assert.That(session.Composition.FieldBindings).IsEmpty();
        await Assert.That(HtmlTreeOperations.FindById(session.Content.Root, addedField.Value.NodeId)).IsNull();

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.FieldBindings.Single().NodeId)
            .IsEqualTo(addedField.Value.NodeId);
    }

    [Test]
    public async Task AddContentField_rejects_a_mismatched_or_missing_scope_without_mutation()
    {
        var session = CreateSession();
        session.AddContentList(42, "article");
        var listScope = session.Composition.ContentLists.Single();
        var template = HtmlTreeOperations.FindById(session.Content.Root, listScope.TemplateRootNodeId)!;
        var originalChildCount = template.Children.Count;
        session.Select(template.NodeId);

        var mismatched = session.AddContentField(99, "title", "text", "Title");

        await Assert.That(mismatched).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(session.Composition.FieldBindings).IsEmpty();
        await Assert.That(template.Children).Count().IsEqualTo(originalChildCount);

        var outsideSession = CreateSession();
        var outside = outsideSession.AddContentField(42, "title", "text", "Title");
        await Assert.That(outside).IsTypeOf<Result<HtmlNode>.Failure>();
        await Assert.That(outsideSession.Content.Root.Children).IsEmpty();
        await Assert.That(outsideSession.CanUndo).IsFalse();
    }

    [Test]
    public async Task UpdateContentListSettings_changes_only_the_sidecar_and_supports_undo_redo()
    {
        var session = CreateSession();
        session.AddContentList(42, "article");
        var initialScope = session.Composition.ContentLists.Single();
        var rootNodeId = session.Content.Root.NodeId;
        var scopeNodeId = session.Content.Root.Children.Single().NodeId;

        var updated = session.UpdateContentListSettings(
            initialScope.NodeId,
            new PageContentListQuery
            {
                PageSize = 24,
                SortField = " publishedOn ",
                SortDirection = PageContentSortDirection.Descending,
                Filters =
                [
                    new PageContentFilter
                    {
                        FieldName = " category ",
                        Operator = PageContentFilterOperator.Equals,
                        Value = " news "
                    }
                ]
            },
            PageContentEmptyStateBehavior.RenderTemplate);

        await Assert.That(updated).IsTypeOf<Result<PageContentListScope>.Ok>();
        var updatedScope = session.Composition.ContentLists.Single();
        await Assert.That(updatedScope.Query.PageSize).IsEqualTo(24);
        await Assert.That(updatedScope.Query.SortField).IsEqualTo("publishedOn");
        await Assert.That(updatedScope.Query.SortDirection).IsEqualTo(PageContentSortDirection.Descending);
        await Assert.That(updatedScope.Query.Filters.Single().FieldName).IsEqualTo("category");
        await Assert.That(updatedScope.Query.Filters.Single().Value).IsEqualTo("news");
        await Assert.That(updatedScope.EmptyState).IsEqualTo(PageContentEmptyStateBehavior.RenderTemplate);
        await Assert.That(session.Content.Root.NodeId).IsEqualTo(rootNodeId);
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scopeNodeId);

        await Assert.That(session.Undo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.ContentLists.Single().Query.PageSize).IsEqualTo(10);
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scopeNodeId);

        await Assert.That(session.Redo()).IsTypeOf<Result<HtmlPageContent>.Ok>();
        await Assert.That(session.Composition.ContentLists.Single().Query.PageSize).IsEqualTo(24);
        await Assert.That(session.Content.Root.Children.Single().NodeId).IsEqualTo(scopeNodeId);
    }

    [Test]
    public async Task UpdateContentListSettings_rejects_unbounded_queries_without_history()
    {
        var scope = HtmlNode.CreateElement("section");
        var template = HtmlNode.CreateElement("article");
        scope.Children.Add(template);
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = scope.NodeId,
                    TemplateRootNodeId = template.NodeId,
                    ContentTypeId = 42,
                    ContentTypeAlias = "article"
                }
            ]
        };
        var session = CreateSession(composition, scope);

        var updated = session.UpdateContentListSettings(
            scope.NodeId,
            new PageContentListQuery
            {
                PageSize = PageContentListQuery.MaximumPageSize + 1,
                Filters = Enumerable.Range(0, PageContentListQuery.MaximumFilterCount + 1)
                    .Select(index => new PageContentFilter
                    {
                        FieldName = $"field{index}",
                        Operator = PageContentFilterOperator.IsNotEmpty
                    })
                    .ToArray()
            },
            PageContentEmptyStateBehavior.RenderNothing);

        await Assert.That(updated).IsTypeOf<Result<PageContentListScope>.Failure>();
        await Assert.That(session.Composition.ContentLists.Single().Query.PageSize).IsEqualTo(10);
        await Assert.That(session.CanUndo).IsFalse();
    }

    [Test]
    public async Task ContentPaletteToken_round_trips_escaped_field_metadata()
    {
        var request = new HtmlContentPaletteRequest
        {
            ItemKind = HtmlPaletteItemKind.ContentField,
            ContentTypeId = 42,
            ContentTypeAlias = "news|stories",
            FieldName = "hero|title",
            FieldType = "text",
            FieldLabel = "Héरो title"
        };

        var parsed = HtmlContentPaletteRequest.TryParse(
            request.ItemKind,
            request.ToToken(),
            out var roundTripped);

        await Assert.That(parsed).IsTrue();
        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped!.ContentTypeAlias).IsEqualTo(request.ContentTypeAlias);
        await Assert.That(roundTripped.FieldName).IsEqualTo(request.FieldName);
        await Assert.That(roundTripped.FieldLabel).IsEqualTo(request.FieldLabel);
    }

    private static HtmlPageEditorSession CreateSession(params HtmlNode[] children)
        => CreateSessionCore(null, children);

    private static HtmlPageEditorSession CreateSession(
        PageCompositionDocument composition,
        params HtmlNode[] children) => CreateSessionCore(composition, children);

    private static HtmlPageEditorSession CreateSessionCore(
        PageCompositionDocument? composition,
        params HtmlNode[] children)
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
            new HtmlComponentCatalog(catalog),
            new NativeCssStyleCompiler(),
            new NativeStyleProfile(),
            composition);
    }

    private static IEnumerable<HtmlNode> Flatten(HtmlNode root)
    {
        yield return root;
        foreach (var child in root.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }
}
