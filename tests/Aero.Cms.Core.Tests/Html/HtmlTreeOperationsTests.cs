using Aero.Cms.Html;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlTreeOperationsTests
{
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
