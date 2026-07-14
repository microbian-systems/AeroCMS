using Aero.Cms.Html;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlElementManifestConformanceTests
{
    private static readonly HashSet<string> KnownStyleCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "layout", "spacing", "surface", "typography"
    };

    [Test]
    public async Task Default_manifest_is_versioned_and_has_canonical_unique_tags()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var definitions = catalog.Definitions.ToArray();

        await Assert.That(catalog.SchemaVersion).IsEqualTo(1);
        await Assert.That(catalog.CatalogVersion).IsEqualTo("2026.1");
        await Assert.That(definitions.Length).IsGreaterThanOrEqualTo(72);
        await Assert.That(definitions.Select(definition => definition.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .IsEqualTo(definitions.Length);
        await Assert.That(definitions.All(definition =>
            !string.IsNullOrWhiteSpace(definition.Tag)
            && definition.Tag == definition.Tag.ToLowerInvariant()
            && definition.Tag.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')))
            .IsTrue();
    }

    [Test]
    public async Task Manifest_references_only_known_elements_and_style_capabilities()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var tags = catalog.Definitions.Select(definition => definition.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencesAreKnown = catalog.Definitions.All(definition =>
            definition.AllowedParentTags.All(tags.Contains)
            && definition.AllowedChildTags.All(tags.Contains));
        var capabilitiesAreKnown = catalog.Definitions.All(definition =>
            definition.StyleCapabilities.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                == definition.StyleCapabilities.Count
            && definition.StyleCapabilities.All(KnownStyleCapabilities.Contains));

        await Assert.That(referencesAreKnown).IsTrue();
        await Assert.That(capabilitiesAreKnown).IsTrue();
    }

    [Test]
    public async Task Void_and_explicit_child_models_are_internally_consistent()
    {
        var definitions = HtmlElementCatalog.CreateDefault().Definitions;

        await Assert.That(definitions
            .Where(definition => definition.IsVoid)
            .All(definition => definition.ChildModel is HtmlChildModel.None
                && definition.AllowedChildTags.Count == 0))
            .IsTrue();
        await Assert.That(definitions
            .Where(definition => definition.AllowedChildTags.Count > 0)
            .All(definition => definition.ChildModel is not HtmlChildModel.None))
            .IsTrue();
        await Assert.That(definitions
            .Where(definition => definition.ChildModel is HtmlChildModel.Text)
            .All(definition => !definition.IsVoid))
            .IsTrue();
    }
}
