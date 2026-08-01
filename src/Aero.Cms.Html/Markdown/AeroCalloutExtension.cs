using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Helpers;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Aero.Cms.Html.MarkdownRendering;

/// <summary>
/// Adds GitHub-style Markdown callouts with Aero's canonical, accessible HTML shape.
/// </summary>
public static class AeroCalloutPipelineExtensions
{
    /// <summary>
    /// Enables <c>&gt; [!NOTE]</c>-style callouts while keeping their source as Markdown.
    /// </summary>
    public static MarkdownPipelineBuilder UseAeroCallouts(this MarkdownPipelineBuilder pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        pipeline.UseAlertBlocks();
        pipeline.Extensions.AddIfNotAlready<AeroCalloutExtension>();
        return pipeline;
    }
}

internal sealed class AeroCalloutExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        renderer.ObjectRenderers.ReplaceOrAdd<AlertBlockRenderer>(new AeroCalloutRenderer());
    }
}

internal sealed class AeroCalloutRenderer : HtmlObjectRenderer<AlertBlock>
{
    private static readonly IReadOnlyDictionary<string, CalloutDescriptor> Callouts =
        new Dictionary<string, CalloutDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOTE"] = new("note", "Note"),
            ["TIP"] = new("tip", "Tip"),
            ["IMPORTANT"] = new("important", "Important"),
            ["WARNING"] = new("warning", "Warning"),
            ["CAUTION"] = new("caution", "Caution")
        };

    protected override void Write(HtmlRenderer renderer, AlertBlock callout)
    {
        var kind = callout.Kind.ToString();
        if (!Callouts.TryGetValue(kind, out var descriptor))
        {
            WriteUnknownAlertAsBlockquote(renderer, callout, kind);
            return;
        }

        renderer.EnsureLine();
        renderer
            .Write("<aside class=\"aero-callout aero-callout-")
            .Write(descriptor.CssSuffix)
            .Write("\" role=\"note\" aria-label=\"")
            .Write(descriptor.Title)
            .WriteLine("\">");
        renderer
            .Write("<p class=\"aero-callout-title\">")
            .Write(descriptor.Title)
            .WriteLine("</p>");

        WriteChildren(renderer, callout);
        renderer.WriteLine("</aside>");
        renderer.EnsureLine();
    }

    private static void WriteUnknownAlertAsBlockquote(
        HtmlRenderer renderer,
        AlertBlock callout,
        string kind)
    {
        renderer.EnsureLine();
        renderer.WriteLine("<blockquote>");
        renderer.Write("<p>[!").WriteEscape(kind).WriteLine("]</p>");
        WriteChildren(renderer, callout);
        renderer.WriteLine("</blockquote>");
        renderer.EnsureLine();
    }

    private static void WriteChildren(HtmlRenderer renderer, AlertBlock callout)
    {
        var implicitParagraph = renderer.ImplicitParagraph;
        renderer.ImplicitParagraph = false;
        renderer.WriteChildren(callout);
        renderer.ImplicitParagraph = implicitParagraph;
    }

    private sealed record CalloutDescriptor(string CssSuffix, string Title);
}
