using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Renders one validated living-standard node for the editor canvas.
/// Browser drag behavior remains outside this component; selection flows upward as an event.
/// </summary>
public sealed class HtmlNodePreview : ComponentBase
{
    [Parameter, EditorRequired]
    public HtmlNode Node { get; set; } = new();

    [Parameter]
    public long? SelectedNodeId { get; set; }

    [Parameter]
    public CompiledPageStyles? CompiledStyles { get; set; }

    [Parameter]
    public bool PreviewMode { get; set; }

    [Parameter]
    public EventCallback<long> NodeSelected { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Node.Kind == HtmlNodeKind.Text)
        {
            builder.AddContent(0, Node.Text);
            return;
        }

        if (Node.Kind == HtmlNodeKind.Fragment)
        {
            RenderChildren(builder);
            return;
        }

        if (Node.Kind != HtmlNodeKind.Element || string.IsNullOrWhiteSpace(Node.TagName))
        {
            return;
        }

        builder.OpenElement(0, Node.TagName);
        builder.SetKey(Node.NodeId);
        AddAttributes(builder);

        if (!PreviewMode)
        {
            builder.AddAttribute(3, "data-aero-node-id", Node.NodeId.ToString());
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                () => NodeSelected.InvokeAsync(Node.NodeId)));
            builder.AddEventStopPropagationAttribute(5, "onclick", true);
            if (Node.TagName is "a" or "button")
            {
                builder.AddEventPreventDefaultAttribute(6, "onclick", true);
            }
        }

        RenderChildren(builder);
        builder.CloseElement();
    }

    private void AddAttributes(RenderTreeBuilder builder)
    {
        foreach (var (name, value) in Node.Attributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals("class", StringComparison.OrdinalIgnoreCase)
                || name.Equals("style", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AddAttribute(10, name, value);
        }

        var classes = new List<string>();
        if (Node.Attributes.TryGetValue("class", out var explicitClasses))
        {
            classes.Add(explicitClasses);
        }
        classes.AddRange(Node.ThemeClasses);
        if (CompiledStyles is not null)
        {
            classes.AddRange(CompiledStyles.ClassesFor(Node.NodeId));
        }
        if (!PreviewMode)
        {
            classes.Add("aero-editor-node");
            if (SelectedNodeId == Node.NodeId)
            {
                classes.Add("aero-editor-node-selected");
            }
        }

        var classValue = string.Join(' ', classes
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal));
        if (!string.IsNullOrWhiteSpace(classValue))
        {
            builder.AddAttribute(2, "class", classValue);
        }
    }

    private void RenderChildren(RenderTreeBuilder builder)
    {
        foreach (var child in Node.Children)
        {
            builder.OpenComponent<HtmlNodePreview>(20);
            builder.SetKey(child.NodeId);
            builder.AddAttribute(21, nameof(Node), child);
            builder.AddAttribute(22, nameof(SelectedNodeId), SelectedNodeId);
            builder.AddAttribute(23, nameof(CompiledStyles), CompiledStyles);
            builder.AddAttribute(24, nameof(PreviewMode), PreviewMode);
            builder.AddAttribute(25, nameof(NodeSelected), NodeSelected);
            builder.CloseComponent();
        }
    }
}
