using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Provides the trusted Markdown rendering policies used by post preview and public delivery.
/// </summary>
internal static class PostMarkdownPipelines
{
    internal static MarkdownPipeline Preview { get; } = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    internal static MarkdownPipeline Public { get; } = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .Build();
}
