using Aero.Cms.Html;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class HtmlStaticRendererTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlStaticRenderer Renderer = new(
        Catalog,
        ContentPolicy,
        AttributePolicy,
        new HtmlContentValidator(Catalog, ContentPolicy, AttributePolicy));

    [Test]
    public async Task Render_emits_encoded_nested_html_and_a_void_element()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.Attributes["id"] = "hero";
        section.ThemeClasses.Add("surface");
        var paragraph = HtmlNode.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Aero < CMS"));
        var image = HtmlNode.CreateElement("img");
        image.Attributes["src"] = "/images/hero.png";
        image.Attributes["alt"] = "Hero";
        section.Children.Add(paragraph);
        section.Children.Add(image);
        content.Root.Children.Add(section);

        var result = Renderer.Render(content);

        var rendered = result as Result<string>.Ok;
        await Assert.That(rendered).IsNotNull();
        await Assert.That(rendered!.Value)
            .IsEqualTo("<section id=\"hero\" class=\"surface\"><p>Aero &lt; CMS</p><img alt=\"Hero\" src=\"/images/hero.png\"></section>");
    }

    [Test]
    public async Task Render_rejects_invalid_nesting_and_unsafe_urls()
    {
        var invalidNesting = new HtmlPageContent();
        var span = HtmlNode.CreateElement("span");
        span.Children.Add(HtmlNode.CreateElement("div"));
        invalidNesting.Root.Children.Add(span);

        var invalidNestingResult = Renderer.Render(invalidNesting);

        await Assert.That(invalidNestingResult).IsTypeOf<Result<string>.Failure>();

        var unsafeUrl = new HtmlPageContent();
        var link = HtmlNode.CreateElement("a");
        link.Attributes["href"] = "javascript:alert(1)";
        link.Children.Add(HtmlNode.CreateText("Unsafe"));
        unsafeUrl.Root.Children.Add(link);

        var unsafeUrlResult = Renderer.Render(unsafeUrl);

        await Assert.That(unsafeUrlResult).IsTypeOf<Result<string>.Failure>();

        var invalidAttribute = new HtmlPageContent();
        var sectionWithLinkAttribute = HtmlNode.CreateElement("section");
        sectionWithLinkAttribute.Attributes["target"] = "_blank";
        invalidAttribute.Root.Children.Add(sectionWithLinkAttribute);

        await Assert.That(Renderer.Render(invalidAttribute)).IsTypeOf<Result<string>.Failure>();

        var invalidMediaUrl = new HtmlPageContent();
        var image = HtmlNode.CreateElement("img");
        image.Attributes["src"] = "mailto:editor@example.com";
        image.Attributes["alt"] = "Invalid media URL";
        invalidMediaUrl.Root.Children.Add(image);

        await Assert.That(Renderer.Render(invalidMediaUrl)).IsTypeOf<Result<string>.Failure>();

        var validNavigationUrl = new HtmlPageContent();
        var emailLink = HtmlNode.CreateElement("a");
        emailLink.Attributes["href"] = "mailto:editor@example.com";
        emailLink.Children.Add(HtmlNode.CreateText("Email the editor"));
        validNavigationUrl.Root.Children.Add(emailLink);

        await Assert.That(Renderer.Render(validNavigationUrl)).IsTypeOf<Result<string>.Ok>();
    }

    [Test]
    public async Task RenderPage_applies_compiled_classes_and_returns_css_separately()
    {
        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        section.ThemeClasses.Add("theme-surface");
        section.Style = new HtmlStyle
        {
            Display = CssDisplay.Grid,
            GridColumns = 2,
            Gap = CssLength.Rem(1)
        };
        content.Root.Children.Add(section);
        var compiled = new NativeCssStyleCompiler().Compile(content, new NativeStyleProfile())
            as Result<CompiledPageStyles>.Ok;

        var result = Renderer.RenderPage(content, compiled!.Value);

        var rendered = result as Result<RenderedHtmlPage>.Ok;
        await Assert.That(rendered).IsNotNull();
        var generatedClass = compiled.Value.ClassesFor(section.NodeId).Single();
        await Assert.That(rendered!.Value.Markup)
            .IsEqualTo($"<section class=\"theme-surface {generatedClass}\"></section>");
        await Assert.That(rendered.Value.CssText).IsEqualTo(compiled.Value.CssText);
        await Assert.That(rendered.Value.StyleContentHash).IsEqualTo(compiled.Value.ContentHash);
    }

    [Test]
    public async Task Render_emits_semantic_table_markup_and_cell_attributes()
    {
        var content = new HtmlPageContent();
        var table = Catalog.CreateElement("table");
        var head = Catalog.CreateElement("thead");
        var row = Catalog.CreateElement("tr");
        var header = Catalog.CreateElement("th");
        header.Attributes["scope"] = "col";
        header.Attributes["colspan"] = "2";
        header.Children.Add(HtmlNode.CreateText("Features"));
        row.Children.Add(header);
        head.Children.Add(row);
        table.Children.Add(head);
        content.Root.Children.Add(table);

        var result = Renderer.Render(content) as Result<string>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value)
            .IsEqualTo("<table><thead><tr><th colspan=\"2\" scope=\"col\">Features</th></tr></thead></table>");
    }

    [Test]
    public async Task Render_emits_encoded_static_form_markup()
    {
        var content = new HtmlPageContent();
        var form = Catalog.CreateElement("form");
        form.Attributes["action"] = "/contact";
        form.Attributes["method"] = "post";
        var label = Catalog.CreateElement("label");
        label.Attributes["for"] = "message";
        label.Children.Add(HtmlNode.CreateText("Message"));
        var textArea = Catalog.CreateElement("textarea");
        textArea.Attributes["id"] = "message";
        textArea.Attributes["name"] = "message";
        textArea.Attributes["rows"] = "4";
        textArea.Children.Add(HtmlNode.CreateText("Aero <CMS>"));
        form.Children.Add(label);
        form.Children.Add(textArea);
        content.Root.Children.Add(form);

        var result = Renderer.Render(content) as Result<string>.Ok;

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value)
            .IsEqualTo("<form action=\"/contact\" method=\"post\"><label for=\"message\">Message</label><textarea id=\"message\" name=\"message\" rows=\"4\">Aero &lt;CMS&gt;</textarea></form>");
    }
}
