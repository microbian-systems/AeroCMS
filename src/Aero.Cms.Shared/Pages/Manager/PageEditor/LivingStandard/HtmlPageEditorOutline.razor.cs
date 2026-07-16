using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Presents the editor's single HTML tree as an accessible navigation outline.
/// </summary>
public partial class HtmlPageEditorOutline
{
    [Parameter, EditorRequired]
    public HtmlPageContent Content { get; set; } = new();

    [Parameter]
    public long? SelectedNodeId { get; set; }

    [Parameter]
    public EventCallback<long?> SelectionChanged { get; set; }

    protected IReadOnlyList<HtmlNode> RootElements => ElementChildren(Content.Root);

    protected IReadOnlyList<HtmlNode> Breadcrumbs => SelectedNodeId is { } nodeId
        ? FindElementPath(Content.Root, nodeId)
        : [];

    protected int ElementCount => CountElements(Content.Root);

    private Task SelectNodeAsync(long nodeId) => SelectionChanged.InvokeAsync(nodeId);

    private static IReadOnlyList<HtmlNode> ElementChildren(HtmlNode node) => node.Children
        .Where(child => child.Kind == HtmlNodeKind.Element)
        .ToArray();

    private static IReadOnlyList<HtmlNode> FindElementPath(HtmlNode root, long nodeId)
    {
        var path = new List<HtmlNode>();
        return FindElementPath(root, nodeId, path) ? path : [];
    }

    private static bool FindElementPath(HtmlNode current, long nodeId, ICollection<HtmlNode> path)
    {
        if (current.Kind == HtmlNodeKind.Element)
        {
            path.Add(current);
        }

        if (current.NodeId == nodeId)
        {
            return current.Kind == HtmlNodeKind.Element;
        }

        foreach (var child in current.Children)
        {
            if (FindElementPath(child, nodeId, path))
            {
                return true;
            }
        }

        if (current.Kind == HtmlNodeKind.Element)
        {
            path.Remove(current);
        }

        return false;
    }

    private static int CountElements(HtmlNode node) => node.Children.Sum(child =>
        (child.Kind == HtmlNodeKind.Element ? 1 : 0) + CountElements(child));
}
