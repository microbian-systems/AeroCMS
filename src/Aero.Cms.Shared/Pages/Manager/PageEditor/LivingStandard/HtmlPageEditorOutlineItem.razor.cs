using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// One element entry in the document outline tree.
/// </summary>
public partial class HtmlPageEditorOutlineItem
{
    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = new();

    [Parameter]
    public long? SelectedNodeId { get; set; }

    [Parameter]
    public int Depth { get; set; }

    [Parameter]
    public EventCallback<long?> NodeSelected { get; set; }

    protected IReadOnlyList<HtmlNode> ElementChildren => Node.Children
        .Where(child => child.Kind == HtmlNodeKind.Element)
        .ToArray();

    protected string ElementLabel => Node.TagName?.ToLowerInvariant() switch
    {
        "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "Heading",
        "p" => "Paragraph",
        "a" => "Link",
        "img" => "Image",
        "button" => "Button",
        "section" => "Section",
        "article" => "Article",
        "div" => "Container",
        _ => "Element"
    };

    protected string AccessibleLabel
    {
        get
        {
            var text = Node.Children
                .FirstOrDefault(child => child.Kind == HtmlNodeKind.Text && !string.IsNullOrWhiteSpace(child.Text))?.Text?
                .Trim();
            return string.IsNullOrWhiteSpace(text)
                ? $"<{Node.TagName}> {ElementLabel}"
                : $"<{Node.TagName}> {text}";
        }
    }

    private Task SelectAsync() => NodeSelected.InvokeAsync(Node.NodeId);
}
