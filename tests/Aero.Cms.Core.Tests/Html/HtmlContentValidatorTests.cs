using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlContentValidatorTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();

    [Test]
    public async Task Validate_accepts_a_supported_ordered_html_tree()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Padding = new CssLogicalSpacing { InlineStart = CssLength.Rem(2) },
            Surface = new CssSurfaceStyle { BackgroundColor = CssColor.Hex("#ffffff") }
        };
        var heading = HtmlNode.CreateElement("h2");
        heading.Style = new HtmlStyle
        {
            Typography = new CssTypographyStyle { FontWeight = 700 }
        };
        heading.Children.Add(HtmlNode.CreateText("A valid page"));
        section.Children.Add(heading);
        content.Root.Children.Add(section);

        var result = CreateValidator().Validate(content);

        await Assert.That(result).IsTypeOf<Result<bool>.Ok>();
    }

    [Test]
    public async Task Validate_rejects_unsupported_attributes_nesting_and_style_capabilities()
    {
        var content = new HtmlPageContent();
        var span = HtmlNode.CreateElement("span");
        span.Attributes["onclick"] = "alert(1)";
        span.Style = new HtmlStyle { Display = CssDisplay.Grid };
        span.Children.Add(HtmlNode.CreateElement("section"));
        content.Root.Children.Add(span);

        var result = CreateValidator().Validate(content);

        await Assert.That(result).IsTypeOf<Result<bool>.Failure>();

        var elementRoot = new HtmlPageContent { Root = HtmlNode.CreateElement("section") };

        await Assert.That(CreateValidator().Validate(elementRoot))
            .IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_rejects_a_shared_node_reference_and_duplicate_identity()
    {
        var content = new HtmlPageContent();
        var first = HtmlNode.CreateElement("section");
        var second = HtmlNode.CreateElement("section");
        second.NodeId = first.NodeId;
        var shared = HtmlNode.CreateElement("p");
        first.Children.Add(shared);
        second.Children.Add(shared);
        content.Root.Children.AddRange([first, second]);

        var result = CreateValidator().Validate(content);

        await Assert.That(result).IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_enforces_depth_and_node_count_limits()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        var child = HtmlNode.CreateElement("div");
        child.Children.Add(HtmlNode.CreateElement("p"));
        section.Children.Add(child);
        content.Root.Children.Add(section);

        var result = CreateValidator(new HtmlContentValidationLimits
        {
            MaximumDepth = 2,
            MaximumNodeCount = 3
        }).Validate(content);

        await Assert.That(result).IsTypeOf<Result<bool>.Failure>();
    }

    private static HtmlContentValidator CreateValidator(HtmlContentValidationLimits? limits = null) =>
        new(Catalog, ContentPolicy, AttributePolicy, limits);
}
