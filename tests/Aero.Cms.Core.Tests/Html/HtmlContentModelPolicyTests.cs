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
        await Assert.That(_catalog.TryGet("table", out var table)).IsTrue();
        await Assert.That(table!.PaletteCategory).IsEqualTo("Tables");
        await Assert.That(table.AllowedChildTags).IsEquivalentTo(["thead", "tbody", "tr"]);
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
        await Assert.That(policy.CanContain(_catalog.CreateElement("section"), _catalog.CreateElement("li")).IsAllowed)
            .IsFalse();
    }

    [Test]
    public async Task Policy_enforces_semantic_table_parent_and_child_relationships()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var table = _catalog.CreateElement("table");
        var head = _catalog.CreateElement("thead");
        var body = _catalog.CreateElement("tbody");
        var row = _catalog.CreateElement("tr");
        var header = _catalog.CreateElement("th");
        var cell = _catalog.CreateElement("td");

        await Assert.That(policy.CanContain(table, head).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(table, body).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(head, row).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(row, header).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(row, cell).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(table, cell).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(row, HtmlNode.CreateText("invalid")).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(HtmlNode.CreateFragment(), body).IsAllowed).IsFalse();
    }

    [Test]
    public async Task Policy_enforces_static_form_content_models()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var form = _catalog.CreateElement("form");
        var label = _catalog.CreateElement("label");
        var input = _catalog.CreateElement("input");
        var textArea = _catalog.CreateElement("textarea");
        var select = _catalog.CreateElement("select");
        var option = _catalog.CreateElement("option");

        await Assert.That(policy.CanContain(form, label).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(form, input).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(form, textArea).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(form, select).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(label, input).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(select, option).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(option, HtmlNode.CreateText("Choice")).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(select, HtmlNode.CreateText("invalid")).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(form, form).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(HtmlNode.CreateFragment(), option).IsAllowed).IsFalse();
    }

    [Test]
    public async Task Policy_enforces_description_list_and_disclosure_relationships()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var descriptionList = _catalog.CreateElement("dl");
        var term = _catalog.CreateElement("dt");
        var description = _catalog.CreateElement("dd");
        var details = _catalog.CreateElement("details");
        var summary = _catalog.CreateElement("summary");

        await Assert.That(policy.CanContain(descriptionList, term).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(descriptionList, description).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(descriptionList, _catalog.CreateElement("div")).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(details, summary).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(details, _catalog.CreateElement("p")).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(_catalog.CreateElement("section"), summary).IsAllowed).IsFalse();
    }

    [Test]
    public async Task Policy_enforces_picture_audio_and_video_children()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var picture = _catalog.CreateElement("picture");
        var audio = _catalog.CreateElement("audio");
        var video = _catalog.CreateElement("video");
        var source = _catalog.CreateElement("source");
        var track = _catalog.CreateElement("track");
        var image = _catalog.CreateElement("img");

        await Assert.That(policy.CanContain(picture, source).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(picture, image).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(audio, source).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(audio, track).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(video, source).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(video, track).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(picture, track).IsAllowed).IsFalse();
        await Assert.That(policy.CanContain(_catalog.CreateElement("section"), source).IsAllowed).IsFalse();
    }

    [Test]
    public async Task Policy_treats_low_risk_semantics_as_flow_and_phrasing_content()
    {
        var policy = new HtmlContentModelPolicy(_catalog);
        var paragraph = _catalog.CreateElement("p");
        var section = _catalog.CreateElement("section");

        foreach (var tag in new[] { "time", "data", "kbd", "samp", "var", "del", "ins", "progress", "meter", "wbr" })
        {
            await Assert.That(policy.CanContain(paragraph, _catalog.CreateElement(tag)).IsAllowed)
                .IsTrue();
        }

        await Assert.That(policy.CanContain(section, _catalog.CreateElement("address")).IsAllowed).IsTrue();
        await Assert.That(policy.CanContain(paragraph, _catalog.CreateElement("address")).IsAllowed).IsFalse();
    }
}
