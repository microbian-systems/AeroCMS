using Aero.Cms.Html;
using Aero.Core.Railway;
using Ganss.Xss;

namespace Aero.Cms.Dependency.Tests;

public sealed class HtmlDependencyCompatibilityTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlContentValidator Validator = new(Catalog, ContentPolicy, AttributePolicy);

    [Test]
    public async Task Html_fragment_importer_accepts_safe_static_markup_from_the_real_html_project()
    {
        var result = CreateImporter().Import("<section class=\"hero\"><h2>Welcome</h2><p>Trusted content.</p></section>");

        var imported = result as Result<HtmlPageContent>.Ok;
        await Assert.That(imported).IsNotNull();
        await Assert.That(imported!.Value.Root.Children.Single().TagName).IsEqualTo("section");
        await Assert.That(Validator.Validate(imported.Value)).IsTypeOf<Result<bool>.Ok>();
    }

    [Test]
    public async Task Html_fragment_importer_rejects_executable_markup_from_the_real_html_project()
    {
        var result = CreateImporter().Import("<section onclick=\"alert(1)\"><script>alert(1)</script></section>");

        await Assert.That(result).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Resolved_sanitizer_removes_executable_markup_and_preserves_safe_structure()
    {
        var sanitizer = new HtmlSanitizer();

        var sanitized = sanitizer.Sanitize("<section><p>Safe</p><script>alert(1)</script><a href=\"javascript:alert(1)\">Unsafe</a></section>");

        await Assert.That(sanitized).Contains("<section>");
        await Assert.That(sanitized).Contains("<p>Safe</p>");
        await Assert.That(sanitized).DoesNotContain("script");
        await Assert.That(sanitized).DoesNotContain("javascript:");
    }

    [Test]
    public async Task Resolved_html_dependency_family_matches_the_approved_compatible_versions()
    {
        await Assert.That(ResolvedPackageVersion("neoui.blazor")).IsEqualTo("4.1.28");
        await Assert.That(ResolvedPackageVersion("neoui.blazor.primitives")).IsEqualTo("4.0.10");
        await Assert.That(ResolvedPackageVersion("htmlsanitizer")).IsEqualTo("9.1.973");
        await Assert.That(ResolvedPackageVersion("anglesharp")).IsEqualTo("1.6.0");
        await Assert.That(ResolvedPackageVersion("anglesharp.css")).IsEqualTo("1.0.0");
    }

    private static HtmlFragmentImporter CreateImporter() => new(
        Catalog,
        AttributePolicy,
        ContentPolicy,
        Validator);

    private static string ResolvedPackageVersion(string packageId)
    {
        var dependencyManifest = Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(HtmlDependencyCompatibilityTests).Assembly.GetName().Name}.deps.json");

        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dependencyManifest));
        var library = document.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Single(property => property.Name.StartsWith($"{packageId}/", StringComparison.OrdinalIgnoreCase));

        return library.Name[(library.Name.LastIndexOf('/') + 1)..];
    }
}
