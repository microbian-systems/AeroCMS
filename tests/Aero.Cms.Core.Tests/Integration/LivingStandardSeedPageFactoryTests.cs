using Aero.Cms.Html;
using Aero.Cms.Modules.Setup;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class LivingStandardSeedPageFactoryTests
{
    [Test]
    public async Task Starter_page_tree_validates_compiles_and_renders_without_framework_classes()
    {
        var catalog = HtmlElementCatalog.CreateDefault();
        var contentPolicy = new HtmlContentModelPolicy(catalog);
        var attributePolicy = new HtmlAttributePolicy();
        var validator = new HtmlContentValidator(catalog, contentPolicy, attributePolicy);
        var content = LivingStandardSeedPageFactory.Create(
            "Starter page",
            "A semantic starter page.",
            [
                ("First section", "First body"),
                ("Second section", "Second body")
            ],
            backgroundImageUrl: "/media/data-center.png",
            callToAction: ("Contact us", "mailto:hello@example.com"));

        var validation = validator.Validate(content);
        if (validation is Result<bool>.Failure { Error: Aero.Core.AeroError.Validation validationError })
        {
            throw new InvalidOperationException(string.Join("; ", validationError.Errors));
        }
        (validation is Result<bool>.Ok).ShouldBeTrue();
        var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile());
        (compiled is Result<CompiledPageStyles>.Ok).ShouldBeTrue();

        var renderer = new HtmlStaticRenderer(catalog, contentPolicy, attributePolicy, validator);
        var rendered = renderer.RenderPage(
            content,
            ((Result<CompiledPageStyles>.Ok)compiled).Value);

        (rendered is Result<RenderedHtmlPage>.Ok).ShouldBeTrue();
        var page = ((Result<RenderedHtmlPage>.Ok)rendered).Value;
        page.Markup.ShouldContain("<main>");
        page.Markup.ShouldContain("<h1");
        page.Markup.ShouldContain("href=\"mailto:hello@example.com\"");
        page.CssText.ShouldContain("url(\"/media/data-center.png\")");
        page.Markup.ShouldNotContain("grid-cols-");
    }
}
