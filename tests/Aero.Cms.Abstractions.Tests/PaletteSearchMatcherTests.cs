using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Abstractions.Tests;

public sealed class PaletteSearchMatcherTests
{
    private static readonly PaletteSearchDocument Document = new(
        "Feature Card",
        "Responsive content container",
        "ui.card",
        "Component",
        "Primitives",
        ["marketing", "callout"]);

    [Test]
    public async Task Empty_query_matches_every_document()
    {
        await Assert.That(PaletteSearchMatcher.Matches(Document, " ")).IsTrue();
    }

    [Test]
    public async Task Query_matches_catalog_fields_case_insensitively()
    {
        await Assert.That(PaletteSearchMatcher.Matches(Document, "RESPONSIVE")).IsTrue();
        await Assert.That(PaletteSearchMatcher.Matches(Document, "ui.card")).IsTrue();
        await Assert.That(PaletteSearchMatcher.Matches(Document, "primitive")).IsTrue();
    }

    [Test]
    public async Task Every_query_term_must_match()
    {
        await Assert.That(PaletteSearchMatcher.Matches(Document, "feature responsive")).IsTrue();
        await Assert.That(PaletteSearchMatcher.Matches(Document, "feature hero")).IsFalse();
    }

    [Test]
    public async Task Query_matches_custom_keywords()
    {
        await Assert.That(PaletteSearchMatcher.Matches(Document, "marketing call")).IsTrue();
    }
}
