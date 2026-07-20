using System.Collections.Generic;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Aero.Cms.Modules.Docs.Areas.Docs.Models;

/// <summary>
/// Extracts second- and third-level headings and Markdig-generated identifiers from Markdown.
/// </summary>
public static class HeadingExtractor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers()
        .Build();

    /// <summary>
    /// Parses Markdown into entries suitable for an on-page table of contents.
    /// </summary>
    /// <param name="markdown">The Markdown source, which may be <see langword="null"/> or blank.</param>
    /// <returns>
    /// H2 and H3 entries in source order. Blank input and headings without generated identifiers
    /// produce no corresponding entries.
    /// </returns>
    /// <remarks>
    /// Anchor identifiers are produced by Markdig's auto-identifier extension. Display text is
    /// taken from the heading's first inline child.
    /// </remarks>
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
