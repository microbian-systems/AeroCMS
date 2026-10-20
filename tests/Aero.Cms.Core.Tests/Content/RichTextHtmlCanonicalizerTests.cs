using Aero.Cms.Html;

namespace Aero.Cms.Core.Tests.Content;

public sealed class RichTextHtmlCanonicalizerTests
{
    [Test]
    [Arguments("bold", "strong")]
    [Arguments("700", "strong")]
    [Arguments("italic", "em")]
    public async Task Converts_editor_inline_emphasis_to_semantic_html(string value, string expectedTag)
    {
        var property = expectedTag == "strong" ? "font-weight" : "font-style";

        var result = RichTextHtmlCanonicalizer.CanonicalizeEditorFormatting(
            $"<span style=\"{property}: {value};\">Okapi</span>");

        await Assert.That(result).Contains($"<{expectedTag}>Okapi</{expectedTag}>");
        await Assert.That(result).DoesNotContain("style=");
    }

    [Test]
    public async Task Converts_editor_text_decoration_and_discards_unapproved_styles()
    {
        var result = RichTextHtmlCanonicalizer.CanonicalizeEditorFormatting(
            "<span style=\"text-decoration: underline line-through; color: red\">Okapi</span>");

        await Assert.That(result).Contains("<u>");
        await Assert.That(result).Contains("<s>");
        await Assert.That(result).DoesNotContain("style=");
        await Assert.That(result).DoesNotContain("color");
    }
}
