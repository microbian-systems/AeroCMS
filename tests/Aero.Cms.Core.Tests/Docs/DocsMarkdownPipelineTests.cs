using Aero.Cms.Modules.Docs;
using Markdig;

namespace Aero.Cms.Core.Tests.Docs;

public sealed class DocsMarkdownPipelineTests
{
    [Test]
    public async Task Public_docs_render_callouts_tables_and_heading_anchors_without_raw_html()
    {
        const string markdown = """
            ## Deployment checklist

            > [!WARNING]
            > Verify the target site before publishing.

            | Check | Status |
            | --- | --- |
            | Site | Ready |

            <script>alert('unsafe')</script>
            """;

        var html = Markdown.ToHtml(markdown, DocsMarkdownPipelines.Public);

        await Assert.That(html).Contains("id=\"deployment-checklist\"");
        await Assert.That(html).Contains(
            """<aside class="aero-callout aero-callout-warning" role="note" aria-label="Warning">""");
        await Assert.That(html).Contains("<table>");
        await Assert.That(html).DoesNotContain("<script");
        await Assert.That(html).Contains("&lt;script&gt;");
    }
}
