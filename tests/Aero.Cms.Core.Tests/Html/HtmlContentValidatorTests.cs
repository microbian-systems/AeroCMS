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

    [Test]
    public async Task Validate_accepts_semantic_tables_and_rejects_invalid_cell_spans()
    {
        var content = new HtmlPageContent();
        var table = Catalog.CreateElement("table");
        var body = Catalog.CreateElement("tbody");
        var row = Catalog.CreateElement("tr");
        var cell = Catalog.CreateElement("td");
        cell.Attributes["colspan"] = "2";
        cell.Children.Add(HtmlNode.CreateText("A valid cell"));
        row.Children.Add(cell);
        body.Children.Add(row);
        table.Children.Add(body);
        content.Root.Children.Add(table);

        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Ok>();

        cell.Attributes["colspan"] = "0";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_accepts_static_forms_and_rejects_nested_forms_and_unsafe_actions()
    {
        var content = new HtmlPageContent();
        var form = Catalog.CreateElement("form");
        form.Attributes["action"] = "/contact";
        form.Attributes["method"] = "post";
        var label = Catalog.CreateElement("label");
        label.Attributes["for"] = "email";
        label.Children.Add(HtmlNode.CreateText("Email"));
        var input = Catalog.CreateElement("input");
        input.Attributes["id"] = "email";
        input.Attributes["type"] = "email";
        input.Attributes["name"] = "email";
        input.Attributes["required"] = string.Empty;
        form.Children.Add(label);
        form.Children.Add(input);
        content.Root.Children.Add(form);

        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Ok>();

        var wrapper = Catalog.CreateElement("div");
        wrapper.Children.Add(Catalog.CreateElement("form"));
        form.Children.Add(wrapper);
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        form.Children.Remove(wrapper);
        form.Attributes["action"] = "javascript:alert(1)";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_accepts_semantic_text_lists_and_disclosure_structure()
    {
        var content = new HtmlPageContent();
        var quote = Catalog.CreateElement("blockquote");
        quote.Attributes["cite"] = "https://example.com/source";
        var quoteText = Catalog.CreateElement("p");
        quoteText.Children.Add(HtmlNode.CreateText("A quotation"));
        quote.Children.Add(quoteText);

        var descriptionList = Catalog.CreateElement("dl");
        var term = Catalog.CreateElement("dt");
        term.Children.Add(HtmlNode.CreateText("Term"));
        var description = Catalog.CreateElement("dd");
        description.Children.Add(HtmlNode.CreateText("Description"));
        descriptionList.Children.Add(term);
        descriptionList.Children.Add(description);

        var details = Catalog.CreateElement("details");
        details.Attributes["open"] = string.Empty;
        var summary = Catalog.CreateElement("summary");
        summary.Children.Add(HtmlNode.CreateText("Summary"));
        details.Children.Add(summary);
        details.Children.Add(Catalog.CreateElement("p"));
        content.Root.Children.AddRange([quote, descriptionList, details]);

        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Ok>();

        details.Children.Add(Catalog.CreateElement("summary"));
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        details.Children.RemoveAt(details.Children.Count - 1);
        details.Children.Reverse();
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        details.Children.Reverse();
        quote.Attributes["cite"] = "javascript:alert(1)";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_accepts_safe_responsive_and_timed_media()
    {
        var content = new HtmlPageContent();
        var picture = Catalog.CreateElement("picture");
        var responsiveSource = Catalog.CreateElement("source");
        responsiveSource.Attributes["srcset"] = "/media/small.jpg 640w, /media/large.jpg 1280w";
        responsiveSource.Attributes["media"] = "(min-width: 40rem)";
        var image = Catalog.CreateElement("img");
        image.Attributes["src"] = "/media/fallback.jpg";
        image.Attributes["alt"] = "Fallback";
        picture.Children.Add(responsiveSource);
        picture.Children.Add(image);

        var video = Catalog.CreateElement("video");
        video.Attributes["controls"] = string.Empty;
        video.Attributes["preload"] = "metadata";
        video.Attributes["poster"] = "/media/poster.jpg";
        var videoSource = Catalog.CreateElement("source");
        videoSource.Attributes["src"] = "/media/video.mp4";
        videoSource.Attributes["type"] = "video/mp4";
        var captions = Catalog.CreateElement("track");
        captions.Attributes["kind"] = "captions";
        captions.Attributes["src"] = "/media/captions.vtt";
        captions.Attributes["srclang"] = "en";
        captions.Attributes["default"] = string.Empty;
        video.Children.Add(videoSource);
        video.Children.Add(captions);
        content.Root.Children.AddRange([picture, video]);

        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Ok>();

        responsiveSource.Attributes["srcset"] = "javascript:alert(1) 2x";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        responsiveSource.Attributes["srcset"] = "/media/small.jpg 640w";
        captions.Attributes["kind"] = "invalid";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();
    }

    [Test]
    public async Task Validate_enforces_progress_and_meter_numeric_invariants()
    {
        var content = new HtmlPageContent();
        var progress = Catalog.CreateElement("progress");
        progress.Attributes["value"] = "0.5";
        progress.Attributes["max"] = "1";
        var meter = Catalog.CreateElement("meter");
        meter.Attributes["value"] = "0.7";
        meter.Attributes["min"] = "0";
        meter.Attributes["max"] = "1";
        meter.Attributes["low"] = "0.3";
        meter.Attributes["high"] = "0.8";
        meter.Attributes["optimum"] = "1";
        content.Root.Children.AddRange([progress, meter]);

        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Ok>();

        progress.Attributes["value"] = "2";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        progress.Attributes["value"] = "0.5";
        meter.Attributes["low"] = "0.9";
        meter.Attributes["high"] = "0.2";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();

        meter.Attributes["low"] = "0.3";
        meter.Attributes["high"] = "0.8";
        meter.Attributes["value"] = "not-a-number";
        await Assert.That(CreateValidator().Validate(content)).IsTypeOf<Result<bool>.Failure>();
    }

    private static HtmlContentValidator CreateValidator(HtmlContentValidationLimits? limits = null) =>
        new(Catalog, ContentPolicy, AttributePolicy, limits);
}
