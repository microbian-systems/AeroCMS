using Aero.Cms.Html.MarkdownRendering;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Provides the trusted Markdown rendering policy used by public documentation.
/// </summary>
internal static class DocsMarkdownPipelines
{
    internal static MarkdownPipeline Public { get; } = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAeroCallouts()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .Build();
}
