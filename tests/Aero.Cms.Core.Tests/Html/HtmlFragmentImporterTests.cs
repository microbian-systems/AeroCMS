using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlFragmentImporterTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlContentValidator Validator = new(Catalog, ContentPolicy, AttributePolicy);

    [Test]
    public async Task Import_converts_supported_static_html_to_a_fresh_valid_tree()
    {
        var result = CreateImporter().Import("<section class=\"hero\"><h2>Welcome</h2><p>Editable content.</p><a href=\"/contact\">Contact us</a></section>");

        var imported = result as Result<HtmlPageContent>.Ok;
        if (result is Result<HtmlPageContent>.Failure failure)
        {
            throw new InvalidOperationException(Describe(failure.Error));
        }
        await Assert.That(imported).IsNotNull();
        await Assert.That(imported!.Value.Root.Children).Count().IsEqualTo(1);

        var section = imported.Value.Root.Children.Single();
        await Assert.That(section.TagName).IsEqualTo("section");
        await Assert.That(section.Attributes["class"]).IsEqualTo("hero");
        await Assert.That(section.Children.Select(child => child.TagName)).IsEquivalentTo(["h2", "p", "a"]);
        await Assert.That(section.Children[0].Children.Single().Text).IsEqualTo("Welcome");
        await Assert.That(Validator.Validate(imported.Value)).IsTypeOf<Result<bool>.Ok>();
        await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(imported.Value.Root)).IsTrue();
    }

    [Test]
    public async Task Import_rejects_parser_recovery_unsafe_attributes_and_unsupported_elements()
    {
        var malformed = CreateImporter().Import("<section><p>Missing close</section>");
        var unsafeAttribute = CreateImporter().Import("<section onclick=\"alert(1)\"><p>Unsafe</p></section>");
        var unsupportedElement = CreateImporter().Import("<script>alert(1)</script>");

        await Assert.That(malformed).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(unsafeAttribute).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(unsupportedElement).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_rejects_content_model_violations_before_returning_a_tree()
    {
        var result = CreateImporter().Import("<span><section><p>Invalid nesting</p></section></span>");

        await Assert.That(result).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_rejects_noncanonical_source_and_duplicate_attributes()
    {
        var upperCase = CreateImporter().Import("<SECTION><p>Uppercase</p></SECTION>");
        var duplicateAttribute = CreateImporter().Import("<section class=\"one\" class=\"two\"><p>Duplicate</p></section>");

        await Assert.That(upperCase).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(duplicateAttribute).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_rejects_nonvoid_self_closing_html_that_would_be_normalized_by_the_parser()
    {
        var paragraph = CreateImporter().Import("<p/>Text");
        var section = CreateImporter().Import("<section/>");

        await Assert.That(paragraph).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(section).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_enforces_source_and_depth_limits()
    {
        var limited = CreateImporter(new HtmlFragmentImportLimits
        {
            MaximumSourceLength = 32,
            MaximumDepth = 2,
            MaximumNodeCount = 10
        });

        var tooLong = limited.Import("<section><p>This source is deliberately too long.</p></section>");
        var tooDeep = CreateImporter(new HtmlFragmentImportLimits
        {
            MaximumSourceLength = 1_000,
            MaximumDepth = 2,
            MaximumNodeCount = 10
        }).Import("<section><div><p>Too deep</p></div></section>");

        await Assert.That(tooLong).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(tooDeep).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    private static HtmlFragmentImporter CreateImporter(HtmlFragmentImportLimits? limits = null) => new(
        Catalog,
        AttributePolicy,
        ContentPolicy,
        Validator,
        limits);

    private static string Describe(Aero.Core.AeroError error) => error switch
    {
        Aero.Core.AeroError.Validation validation => string.Join("; ", validation.Errors),
        _ => error.ToString()
    };
}
