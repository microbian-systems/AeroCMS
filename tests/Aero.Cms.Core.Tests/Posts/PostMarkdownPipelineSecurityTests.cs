using Aero.Cms.Modules.Posts;
using Markdig;

namespace Aero.Cms.Core.Tests.Posts;

public sealed class PostMarkdownPipelineSecurityTests
{
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
}
