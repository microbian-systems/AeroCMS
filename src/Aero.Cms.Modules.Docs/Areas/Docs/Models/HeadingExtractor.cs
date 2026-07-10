using System.Collections.Generic;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Extracts H2 and H3 headings from Markdown content using
/// Markdig's AST with auto-identifier pipeline for "On This Page" navigation.
/// </summary>
public static class HeadingExtractor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers()
        .Build();

        /// <summary>
    /// Extract method.
    /// </summary>
public static List<HeadingItem> Extract(string? markdown)
    {
        var headings = new List<HeadingItem>();

        if (string.IsNullOrWhiteSpace(markdown))
            return headings;

        var document = Markdown.Parse(markdown, Pipeline);

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.Level != 2 && heading.Level != 3)
                continue;

            var id = heading.TryGetAttributes()?.Id;
            if (string.IsNullOrEmpty(id))
                continue;

            var text = heading.Inline?.FirstChild?.ToString() ?? string.Empty;

            headings.Add(new HeadingItem
            {
                Text = text,
                AnchorId = id,
                Level = heading.Level
            });
        }

        return headings;
    }
}
