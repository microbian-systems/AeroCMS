using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Html;

public sealed class MarkdownInterchangeAdapterTests
{
    private static readonly HtmlElementCatalog Catalog = HtmlElementCatalog.CreateDefault();
    private static readonly HtmlContentModelPolicy ContentPolicy = new(Catalog);
    private static readonly HtmlAttributePolicy AttributePolicy = new();
    private static readonly HtmlContentValidator Validator = new(Catalog, ContentPolicy, AttributePolicy);

    [Test]
    public async Task Import_converts_common_markdown_through_the_html_policy_boundary()
    {
        const string markdown = """
            # Welcome

            Paragraph with **strong**, *emphasis*, ~~old text~~, [documentation](/docs), and `inline code`.

            > A quoted paragraph.

            - First item
            - Second item

            ![Sable logo](/media/sable.png "Sable")

            ```csharp
            Console.WriteLine("Hello");
            ```
            """;

        var result = CreateAdapter().Import(markdown);
        var imported = RequireOk(result);

        await Assert.That(Validator.Validate(imported)).IsTypeOf<Result<bool>.Ok>();
        await Assert.That(HtmlTreeOperations.HasUniqueNodeIds(imported.Root)).IsTrue();
        await Assert.That(imported.Root.Children.Select(node => node.TagName!))
            .IsEquivalentTo(["h1", "p", "blockquote", "ul", "p", "pre"]);

        var paragraph = imported.Root.Children[1];
        await Assert.That(Descendants(paragraph).Select(node => node.TagName).Where(tag => tag is not null))
            .Contains("strong")
            .And.Contains("em")
            .And.Contains("del")
            .And.Contains("a")
            .And.Contains("code");

        var image = Descendants(imported.Root).Single(node => node.TagName == "img");
        await Assert.That(image.Attributes["src"]).IsEqualTo("/media/sable.png");
        await Assert.That(image.Attributes["alt"]).IsEqualTo("Sable logo");

        var fencedCode = imported.Root.Children[^1].Children.Single();
        await Assert.That(fencedCode.TagName).IsEqualTo("code");
        await Assert.That(fencedCode.Attributes["class"]).IsEqualTo("language-csharp");
    }

    [Test]
    public async Task Import_treats_raw_html_as_literal_text()
    {
        var imported = RequireOk(CreateAdapter().Import("<script>alert('safe')</script>"));

        await Assert.That(Descendants(imported.Root).Any(node => node.TagName == "script")).IsFalse();
        await Assert.That(string.Concat(Descendants(imported.Root)
            .Where(node => node.Kind is HtmlNodeKind.Text)
            .Select(node => node.Text)))
            .Contains("<script>")
            .And.Contains("</script>");
    }

    [Test]
    public async Task Import_rejects_unsafe_link_destinations()
    {
        var result = CreateAdapter().Import("[Unsafe](javascript:alert%281%29)");

        await Assert.That(result).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_rejects_blank_and_oversized_markdown()
    {
        var adapter = CreateAdapter(new MarkdownInterchangeLimits
        {
            MaximumMarkdownLength = 8,
            MaximumGeneratedHtmlLength = 1_000,
            MaximumExportLength = 1_000
        });

        await Assert.That(adapter.Import("   ")).IsTypeOf<Result<HtmlPageContent>.Failure>();
        await Assert.That(adapter.Import("This is too long.")).IsTypeOf<Result<HtmlPageContent>.Failure>();
    }

    [Test]
    public async Task Import_accepts_a_commonmark_ordered_list_starting_at_zero()
    {
        var imported = RequireOk(CreateAdapter().Import("0. Zero\n1. One"));
        var list = imported.Root.Children.Single();

        await Assert.That(list.TagName).IsEqualTo("ol");
        await Assert.That(list.Attributes["start"]).IsEqualTo("0");
    }

    [Test]
    public async Task Export_round_trips_the_supported_semantic_subset()
    {
        const string source = """
            ## Portable content

            A paragraph with **strong text**, *emphasis*, [a link](/guide), and `code`.

            3. Third
            4. Fourth

            > Quoted content.

            ```json
            { "enabled": true }
            ```
            """;

        var adapter = CreateAdapter();
        var imported = RequireOk(adapter.Import(source));
        var exported = adapter.Export(imported);
        var markdown = RequireOk(exported);
        var roundTripped = RequireOk(adapter.Import(markdown));

        await Assert.That(markdown)
            .Contains("## Portable content")
            .And.Contains("3. Third")
            .And.Contains("```json");

        var renderer = new HtmlStaticRenderer(Catalog, ContentPolicy, AttributePolicy, Validator);
        await Assert.That(RequireOk(renderer.Render(roundTripped)))
            .IsEqualTo(RequireOk(renderer.Render(imported)));
    }

    [Test]
    public async Task Export_round_trips_the_normalized_tiptap_toolbar_contract()
    {
        var importer = new HtmlFragmentImporter(
            Catalog,
            AttributePolicy,
            ContentPolicy,
            Validator);
        var renderer = new HtmlStaticRenderer(Catalog, ContentPolicy, AttributePolicy, Validator);
        var cases = new Dictionary<string, string>
        {
            ["inline formatting"] =
                """<h2>Toolbar content</h2><p><strong>Bold</strong>, <em>italic</em>, <del>struck</del>, <code>inline</code>, and <a href="/guide">linked</a>.</p>""",
            ["lists"] =
                """<ul><li>Bullet item</li></ul><ol><li>Numbered item</li></ol>""",
            ["blockquote"] =
                """<blockquote><p>Quoted content.</p></blockquote>""",
            ["code block"] =
                "<pre><code>Console.WriteLine(\"Hello\");\n</code></pre>",
            ["image"] =
                """<p><img src="/media/example.jpg" alt="Example" title="A title"></p>""",
            ["horizontal rule"] =
                """<hr>"""
        };

        foreach (var (name, tiptapHtml) in cases)
        {
            var imported = RequireOk(importer.Import(tiptapHtml));
            var exported = CreateAdapter().Export(imported);
            if (exported is Result<string>.Failure failure)
            {
                throw new InvalidOperationException($"{name}: {Describe(failure.Error)}");
            }

            var markdown = ((Result<string>.Ok)exported).Value;
            var roundTripped = RequireOk(CreateAdapter().Import(markdown));
            await Assert.That(RequireOk(renderer.Render(roundTripped)))
                .IsEqualTo(RequireOk(renderer.Render(imported)));
        }
    }

    [Test]
    public async Task Interchange_round_trips_canonical_pipe_tables()
    {
        const string markdown = """
            | Feature | Status |
            | --- | --- |
            | Images | Ready |
            | Tables | Ready |
            """;

        var adapter = CreateAdapter();
        var imported = RequireOk(adapter.Import(markdown));
        var table = imported.Root.Children.Single();

        await Assert.That(table.TagName).IsEqualTo("table");
        await Assert.That(table.Children.Select(node => node.TagName!))
            .IsEquivalentTo(["thead", "tbody"]);

        var exported = RequireOk(adapter.Export(imported));
        await Assert.That(exported).Contains("| Feature | Status |");
        await Assert.That(exported).Contains("| Images | Ready |");

        var renderer = new HtmlStaticRenderer(Catalog, ContentPolicy, AttributePolicy, Validator);
        await Assert.That(RequireOk(renderer.Render(RequireOk(adapter.Import(exported)))))
            .IsEqualTo(RequireOk(renderer.Render(imported)));
    }

    [Test]
    public async Task Interchange_preserves_link_titles_and_canonical_callout_markers()
    {
        const string markdown = """
            Read the [installation guide](/docs/getting-started "Install AeroCMS").

            > [!NOTE]
            >
            > Save the document before publishing it.
            """;

        var adapter = CreateAdapter();
        var imported = RequireOk(adapter.Import(markdown));
        var exported = RequireOk(adapter.Export(imported));

        await Assert.That(exported)
            .Contains("""[installation guide](</docs/getting-started> "Install AeroCMS")""")
            .And.Contains("> [!NOTE]")
            .And.Contains("> Save the document before publishing it.");

        var renderer = new HtmlStaticRenderer(Catalog, ContentPolicy, AttributePolicy, Validator);
        await Assert.That(RequireOk(renderer.Render(RequireOk(adapter.Import(exported)))))
            .IsEqualTo(RequireOk(renderer.Render(imported)));
    }

    [Test]
    public async Task Export_rejects_table_spans_and_non_rectangular_rows()
    {
        var importer = new HtmlFragmentImporter(
            Catalog,
            AttributePolicy,
            ContentPolicy,
            Validator);
        var adapter = CreateAdapter();

        var spanning = RequireOk(importer.Import(
            """<table><thead><tr><th colspan="2">Header</th></tr></thead><tbody><tr><td>A</td><td>B</td></tr></tbody></table>"""));
        await Assert.That(adapter.Export(spanning)).IsTypeOf<Result<string>.Failure>();

        var uneven = RequireOk(importer.Import(
            """<table><thead><tr><th>A</th><th>B</th></tr></thead><tbody><tr><td>Only one</td></tr></tbody></table>"""));
        await Assert.That(adapter.Export(uneven)).IsTypeOf<Result<string>.Failure>();
    }

    [Test]
    public async Task Export_fails_instead_of_dropping_layout_style_or_attributes()
    {
        var section = Catalog.CreateElement("section");
        section.Style = new HtmlStyle { Display = CssDisplay.Grid };
        section.Attributes["data-layout"] = "hero";
        var content = new HtmlPageContent();
        content.Root.Children.Add(section);

        var result = CreateAdapter().Export(content);

        await Assert.That(result).IsTypeOf<Result<string>.Failure>();
        var failure = (Result<string>.Failure)result;
        await Assert.That(Describe(failure.Error)).Contains("cannot preserve");
    }

    [Test]
    public async Task Export_rejects_empty_blocks_and_non_canonical_semantic_aliases()
    {
        await Assert.That(CreateAdapter().Export(new HtmlPageContent()))
            .IsTypeOf<Result<string>.Failure>();

        var emptyParagraph = new HtmlPageContent();
        emptyParagraph.Root.Children.Add(Catalog.CreateElement("p"));
        await Assert.That(CreateAdapter().Export(emptyParagraph))
            .IsTypeOf<Result<string>.Failure>();

        foreach (var tag in new[] { "b", "i", "s" })
        {
            var content = new HtmlPageContent();
            var paragraph = Catalog.CreateElement("p");
            var alias = HtmlNode.CreateElement(tag);
            alias.Children.Add(HtmlNode.CreateText("Text"));
            paragraph.Children.Add(alias);
            content.Root.Children.Add(paragraph);

            await Assert.That(CreateAdapter().Export(content))
                .IsTypeOf<Result<string>.Failure>();
        }
    }

    [Test]
    public async Task Export_verifies_whitespace_and_code_blocks_by_round_tripping()
    {
        var whitespace = new HtmlPageContent();
        var paragraph = Catalog.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Trailing text "));
        whitespace.Root.Children.Add(paragraph);
        await Assert.That(CreateAdapter().Export(whitespace))
            .IsTypeOf<Result<string>.Failure>();

        var codeContent = new HtmlPageContent();
        var pre = Catalog.CreateElement("pre");
        var code = Catalog.CreateElement("code");
        code.Children.Add(HtmlNode.CreateText("line\n\n"));
        pre.Children.Add(code);
        codeContent.Root.Children.Add(pre);
        await Assert.That(CreateAdapter().Export(codeContent))
            .IsTypeOf<Result<string>.Failure>();
    }

    [Test]
    public async Task Export_rejects_noncanonical_boundary_whitespace_inside_inline_marks()
    {
        var importer = new HtmlFragmentImporter(
            Catalog,
            AttributePolicy,
            ContentPolicy,
            Validator);
        var content = RequireOk(importer.Import(
            """<p>Before<strong> bold text </strong>after</p>"""));

        var result = CreateAdapter().Export(content);

        await Assert.That(result).IsTypeOf<Result<string>.Failure>();
        var failure = (Result<string>.Failure)result;
        await Assert.That(Describe(failure.Error))
            .Contains("cannot be represented losslessly");
    }

    [Test]
    public async Task Interchange_enforces_generated_html_and_export_limits()
    {
        var importAdapter = CreateAdapter(new MarkdownInterchangeLimits
        {
            MaximumMarkdownLength = 100,
            MaximumGeneratedHtmlLength = 5,
            MaximumExportLength = 100
        });
        await Assert.That(importAdapter.Import("# Heading"))
            .IsTypeOf<Result<HtmlPageContent>.Failure>();

        var exportContent = new HtmlPageContent();
        var paragraph = Catalog.CreateElement("p");
        paragraph.Children.Add(HtmlNode.CreateText("Long export content"));
        exportContent.Root.Children.Add(paragraph);
        var exportAdapter = CreateAdapter(new MarkdownInterchangeLimits
        {
            MaximumMarkdownLength = 100,
            MaximumGeneratedHtmlLength = 100,
            MaximumExportLength = 8
        });
        await Assert.That(exportAdapter.Export(exportContent))
            .IsTypeOf<Result<string>.Failure>();
    }

    private static MarkdownInterchangeAdapter CreateAdapter(MarkdownInterchangeLimits? limits = null)
    {
        var htmlLimits = limits is null
            ? null
            : new HtmlFragmentImportLimits
            {
                MaximumSourceLength = limits.MaximumGeneratedHtmlLength,
                MaximumDepth = 64,
                MaximumNodeCount = 5_000
            };
        var htmlImporter = new HtmlFragmentImporter(
            Catalog,
            AttributePolicy,
            ContentPolicy,
            Validator,
            htmlLimits);
        return new MarkdownInterchangeAdapter(htmlImporter, Validator, limits);
    }

    private static T RequireOk<T>(Result<T> result) => result switch
    {
        Result<T>.Ok ok => ok.Value,
        Result<T>.Failure failure => throw new InvalidOperationException(Describe(failure.Error)),
        _ => throw new InvalidOperationException("Unknown result state.")
    };

    private static IEnumerable<HtmlNode> Descendants(HtmlNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static string Describe(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        _ => error.ToString()
    };
}
