using Aero.Cms.Modules.Posts;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace Aero.Cms.Core.Tests.Posts;

public sealed class PostMarkdownPipelineSecurityTests
{
    private static readonly string[] SupportedCallouts =
        ["NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION"];

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Post_markdown_disables_raw_html_and_preserves_pipe_tables(bool publicPipeline)
    {
        const string markdown = """
            <script>alert('unsafe')</script>

            <img src="x" onerror="alert('unsafe')">

            | Name | Value |
            | --- | --- |
            | Safe | Content |
            """;

        var pipeline = publicPipeline
            ? PostMarkdownPipelines.Public
            : PostMarkdownPipelines.Preview;
        var html = Markdown.ToHtml(markdown, pipeline);

        await Assert.That(html).DoesNotContain("<script");
        await Assert.That(html).DoesNotContain("<img");
        await Assert.That(html).Contains("&lt;script&gt;");
        await Assert.That(html).Contains("<table>");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Post_markdown_renders_supported_callouts_with_bounded_accessible_markup(
        bool publicPipeline)
    {
        var markdown = string.Join(
            Environment.NewLine + Environment.NewLine,
            SupportedCallouts.Select(kind => $"""
                > [!{kind}]
                > A {kind.ToLowerInvariant()} with **formatted content**.
                """));

        var pipeline = publicPipeline
            ? PostMarkdownPipelines.Public
            : PostMarkdownPipelines.Preview;
        var html = Markdown.ToHtml(markdown, pipeline);

        foreach (var kind in SupportedCallouts)
        {
            var suffix = kind.ToLowerInvariant();
            var title = char.ToUpperInvariant(suffix[0]) + suffix[1..];

            await Assert.That(html)
                .Contains($"""<aside class="aero-callout aero-callout-{suffix}" role="note" aria-label="{title}">""");
            await Assert.That(html).Contains($"""<p class="aero-callout-title">{title}</p>""");
        }

        await Assert.That(html).DoesNotContain("[!NOTE]");
        await Assert.That(html).Contains("<strong>formatted content</strong>");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Post_markdown_does_not_create_unbounded_classes_for_unknown_callouts(
        bool publicPipeline)
    {
        const string markdown = """
            > [!CUSTOM]
            > Keep this as an ordinary quotation.
            """;

        var pipeline = publicPipeline
            ? PostMarkdownPipelines.Public
            : PostMarkdownPipelines.Preview;
        var html = Markdown.ToHtml(markdown, pipeline);

        await Assert.That(html).Contains("<blockquote>");
        await Assert.That(html).Contains("[!CUSTOM]");
        await Assert.That(html).DoesNotContain("aero-callout-custom");
        await Assert.That(html).DoesNotContain("markdown-alert-custom");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Callout_extension_does_not_change_existing_link_rendering(bool publicPipeline)
    {
        const string markdown = """
            [HTTPS](https://example.com/docs "Documentation")

            [Existing scheme behavior](javascript:alert(1))
            """;

        var baselineBuilder = new MarkdownPipelineBuilder()
            .DisableHtml();
        if (publicPipeline)
        {
            baselineBuilder.UseAutoIdentifiers();
        }

        var baseline = baselineBuilder
            .UsePipeTables()
            .Build();
        var expected = Markdown.ToHtml(markdown, baseline);
        var actual = Markdown.ToHtml(
            markdown,
            publicPipeline ? PostMarkdownPipelines.Public : PostMarkdownPipelines.Preview);

        await Assert.That(actual).IsEqualTo(expected);
    }
}
