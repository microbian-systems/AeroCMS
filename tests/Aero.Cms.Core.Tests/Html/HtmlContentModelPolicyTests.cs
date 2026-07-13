using Aero.Cms.Html;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlContentModelPolicyTests
{
    private readonly HtmlElementCatalog _catalog = HtmlElementCatalog.CreateDefault();

    [Test]
    public async Task Catalog_exposes_the_first_release_structural_and_content_elements()
    {
        await Assert.That(_catalog.TryGet("section", out var section)).IsTrue();
        await Assert.That(section!.PaletteCategory).IsEqualTo("Structural");
        await Assert.That(_catalog.TryGet("p", out var paragraph)).IsTrue();
        await Assert.That(paragraph!.ChildModel).IsEqualTo(HtmlChildModel.Phrasing);
    }

    [Test]
    public async Task Policy_allows_a_valid_section_paragraph_text_tree()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var root = HtmlNode.CreateFragment();
        var section = _catalog.CreateElement("section");
        var paragraph = _catalog.CreateElement("p");
        var text = HtmlNode.CreateText("Welcome");

        await Assert.That(policy.CanContain(root, section).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(section, paragraph).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(paragraph, text).IsAllowed).IsTrue();
    }

    [Test]
    public async Task Policy_rejects_invalid_block_list_void_and_interactive_nesting()
    {
        var policy = new HtmlContentModelPolicy(_catalog);

        var spanDecision = policy.CanContain(_catalog.CreateElement("span"), _catalog.CreateElement("div"));
        var listDecision = policy.CanContain(_catalog.CreateElement("ul"), _catalog.CreateElement("div"));
        var imageDecision = policy.CanContain(_catalog.CreateElement("img"), HtmlNode.CreateText("Nope"));
        var anchorDecision = policy.CanContain(_catalog.CreateElement("a"), _catalog.CreateElement("button"));

        await Assert.That(spanDecision.IsAllowed).IsFalse();
        await Assert.That(listDecision.IsAllowed).IsFalse();
        await Assert.That(imageDecision.IsAllowed).IsFalse();
        await Assert.That(anchorDecision.IsAllowed).IsFalse();
    }
}
