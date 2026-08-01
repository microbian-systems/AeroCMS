using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlTreeOperationsTests
{
    [Test]
    public async Task Content_history_restores_an_independent_snapshot_and_supports_redo()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        content.Root.Children.Add(section);
        var history = new HtmlPageContentHistory();

        history.CaptureBeforeChange(content);
        section.Children.Add(HtmlNode.CreateElement("p"));

        var undo = history.Undo(content);

        var undone = undo as Result<HtmlPageContent>.Ok;
        await Assert.That(undone).IsNotNull();
        await Assert.That(undone!.Value.Root.Children[0].Children).IsEmpty();
        await Assert.That(history.CanRedo).IsTrue();

        var redo = history.Redo(undone.Value);

        var redone = redo as Result<HtmlPageContent>.Ok;
        await Assert.That(redone).IsNotNull();
        await Assert.That(redone!.Value.Root.Children[0].Children).Count().IsEqualTo(1);
        await Assert.That(redone.Value.Root.Children[0]).IsNotSameReferenceAs(section);
    }

    [Test]
    public async Task Clone_preserving_node_ids_creates_an_independent_publication_snapshot()
    {
        var source = HtmlNode.CreateElement("section");
        source.Attributes["id"] = "hero";
        source.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1.5m),
            Padding = new CssLogicalSpacing { InlineStart = CssLength.Rem(2) },
            Surface = new CssSurfaceStyle
            {
                BackgroundColor = CssColor.Token("surface-primary"),
                BackgroundImageUrl = "/images/hero.jpg",
                BorderRadius = CssLength.Rem(1)
            },
            Typography = new CssTypographyStyle
            {
                FontSize = CssLength.Rem(2),
                FontWeight = 700,
                Gradient = new CssTextGradient
                {
                    StartColor = CssColor.Token("text-accent-start"),
                    EndColor = CssColor.Hex("#ffffff"),
                    AngleDegrees = 45
                }
            }
        };
        source.Children.Add(HtmlNode.CreateElement("p"));

        var snapshot = HtmlTreeOperations.ClonePreservingNodeIds(source);

        await Assert.That(snapshot.NodeId).IsEqualTo(source.NodeId);
        await Assert.That(snapshot.Children[0].NodeId).IsEqualTo(source.Children[0].NodeId);
        await Assert.That(snapshot.Attributes).IsNotSameReferenceAs(source.Attributes);
        await Assert.That(snapshot.Children).IsNotSameReferenceAs(source.Children);
        await Assert.That(snapshot.Style).IsNotSameReferenceAs(source.Style);
        await Assert.That(snapshot.Style!.Gap).IsNotSameReferenceAs(source.Style!.Gap);
        await Assert.That(snapshot.Style.Padding).IsNotSameReferenceAs(source.Style.Padding);
        await Assert.That(snapshot.Style.Surface).IsNotSameReferenceAs(source.Style.Surface);
        await Assert.That(snapshot.Style.Surface!.BackgroundColor)
            .IsNotSameReferenceAs(source.Style.Surface!.BackgroundColor);
        await Assert.That(snapshot.Style.Typography).IsNotSameReferenceAs(source.Style.Typography);
        await Assert.That(snapshot.Style.Typography!.Gradient).IsNotSameReferenceAs(source.Style.Typography!.Gradient);
        await Assert.That(snapshot.Style.Typography.Gradient!.StartColor)
            .IsNotSameReferenceAs(source.Style.Typography.Gradient!.StartColor);
        await Assert.That(snapshot.Style.GridColumns).IsEqualTo(2);
    }

    [Test]
    public async Task Clone_with_fresh_node_ids_preserves_structure_without_aliasing()
    {
        var root = HtmlNode.CreateFragment();
        var section = HtmlNode.CreateElement("section");
        section.Attributes["aria-label"] = "Introduction";
        section.ThemeClasses.Add("theme-surface");
        section.Children.Add(HtmlNode.CreateText("Welcome"));
        root.Children.Add(section);

        var clone = HtmlTreeOperations.CloneWithFreshNodeIds(root);

        await Assert.That(clone.NodeId).IsNotEqualTo(root.NodeId);
        await Assert.That(clone.Children[0].NodeId).IsNotEqualTo(section.NodeId);
        await Assert.That(clone.Children[0].Children[0].NodeId)
            .IsNotEqualTo(section.Children[0].NodeId);
        await Assert.That(clone.Children[0].TagName).IsEqualTo("section");
        await Assert.That(clone.Children[0].Attributes["aria-label"]).IsEqualTo("Introduction");
        await Assert.That(clone.Children[0].ThemeClasses).IsEquivalentTo(["theme-surface"]);
        await Assert.That(clone.Children[0].Children[0].Text).IsEqualTo("Welcome");

        clone.Children[0].Attributes["aria-label"] = "Changed";
        clone.Children[0].ThemeClasses[0] = "theme-alternate";

        await Assert.That(section.Attributes["aria-label"]).IsEqualTo("Introduction");
        await Assert.That(section.ThemeClasses[0]).IsEqualTo("theme-surface");
    }

    [Test]
    public async Task Find_by_id_locates_a_deeply_nested_node()
    {
        var root = HtmlNode.CreateFragment();
        var section = HtmlNode.CreateElement("section");
        var paragraph = HtmlNode.CreateElement("p");
        var text = HtmlNode.CreateText("Nested text");
        paragraph.Children.Add(text);
        section.Children.Add(paragraph);
        root.Children.Add(section);

        var found = HtmlTreeOperations.FindById(root, text.NodeId);

        await Assert.That(found).IsSameReferenceAs(text);
    }
}
