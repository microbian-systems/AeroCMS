using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using System.Globalization;

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

    [Parameter, EditorRequired]
    public HtmlElementCatalog Catalog { get; set; } = null!;

    [Parameter, EditorRequired]
    public IHtmlContentModelPolicy ContentPolicy { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<long, PageRenderedFragmentKind> RenderedFragmentKinds { get; set; } =
        new Dictionary<long, PageRenderedFragmentKind>();

    [Parameter]
    public HtmlNode? MoveSourceNode { get; set; }

    [Parameter]
    public bool CanAcceptMoveAsSibling { get; set; }

    [Parameter]
    public bool PreviewMode { get; set; }

    [Parameter]
    public EventCallback<long> NodeSelected { get; set; }

    [Parameter]
    public EventCallback<long> NodeEditRequested { get; set; }

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
        var renderedFragmentKind = !PreviewMode
            && RenderedFragmentKinds.TryGetValue(Node.NodeId, out var fragmentKind)
                ? fragmentKind
                : (PageRenderedFragmentKind?)null;
        AddAttributes(builder, renderedFragmentKind);

        if (!PreviewMode)
        {
            builder.AddAttribute(4, "data-aero-node-id", Node.NodeId.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                () => NodeSelected.InvokeAsync(Node.NodeId)));
            builder.AddEventStopPropagationAttribute(6, "onclick", true);
            if (Node.TagName is "a" or "button")
            {
                builder.AddEventPreventDefaultAttribute(7, "onclick", true);
            }

            builder.AddAttribute(8, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(
                this,
                () => NodeEditRequested.InvokeAsync(Node.NodeId)));
            builder.AddEventStopPropagationAttribute(9, "ondblclick", true);
            if (Node.TagName is "a" or "button")
            {
                builder.AddEventPreventDefaultAttribute(10, "ondblclick", true);
            }

            builder.AddAttribute(12, "data-aero-sortable-node", "true");
            builder.AddAttribute(
                13,
                "data-aero-can-have-children",
                CanHaveChildren() ? "true" : "false");
            builder.AddAttribute(
                14,
                "data-aero-can-accept-selected-inside",
                CanAcceptMoveInside() ? "true" : "false");
            builder.AddAttribute(
                15,
                "data-aero-can-accept-selected-as-sibling",
                CanAcceptMoveAsSibling ? "true" : "false");
        }

        if (renderedFragmentKind is { } kind)
        {
            RenderRenderedFragmentPlaceholder(builder, kind);
        }
        else
        {
            RenderChildren(builder);
        }
        builder.CloseElement();
    }

    private void AddAttributes(
        RenderTreeBuilder builder,
        PageRenderedFragmentKind? renderedFragmentKind)
    {
        foreach (var (name, value) in Node.Attributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (name.Equals("class", StringComparison.OrdinalIgnoreCase)
                || name.Equals("style", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.AddAttribute(1, name, value);
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
            if (IsStructuralContainer())
            {
                classes.Add("aero-editor-node-container");
            }

            if (renderedFragmentKind is not null)
            {
                classes.Add("aero-editor-node-rendered-fragment");
                builder.AddAttribute(
                    2,
                    "data-aero-fragment-kind",
                    FragmentKindToken(renderedFragmentKind.Value));
            }

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
            builder.AddAttribute(3, "class", classValue);
        }
    }

    private void RenderRenderedFragmentPlaceholder(
        RenderTreeBuilder builder,
        PageRenderedFragmentKind kind)
    {
        var presentation = FragmentPresentation.For(kind);

        builder.OpenElement(40, "button");
        builder.AddAttribute(41, "type", "button");
        builder.AddAttribute(42, "class", "aero-editor-fragment-placeholder");
        builder.AddAttribute(43, "aria-label", $"Edit {presentation.Label} block");
        builder.AddAttribute(44, "title", $"Edit {presentation.Label} block");
        builder.AddAttribute(
            45,
            "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, EditRenderedFragmentAsync));
        builder.AddEventStopPropagationAttribute(46, "onclick", true);

        builder.OpenElement(47, "span");
        builder.AddAttribute(48, "class", "aero-editor-fragment-placeholder__icon");
        builder.AddAttribute(49, "aria-hidden", "true");
        builder.AddContent(50, presentation.Icon);
        builder.CloseElement();

        builder.OpenElement(51, "span");
        builder.AddAttribute(52, "class", "aero-editor-fragment-placeholder__content");
        builder.OpenElement(53, "strong");
        builder.AddContent(54, $"{presentation.Label} block");
        builder.CloseElement();
        builder.OpenElement(55, "span");
        builder.AddContent(56, "Click to edit");
        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(57, "span");
        builder.AddAttribute(58, "class", "aero-editor-fragment-placeholder__action");
        builder.AddAttribute(59, "aria-hidden", "true");
        builder.AddContent(60, "Edit");
        builder.OpenElement(61, "span");
        builder.AddAttribute(62, "class", "aero-editor-fragment-placeholder__arrow");
        builder.AddContent(63, "→");
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();
    }

    private async Task EditRenderedFragmentAsync()
    {
        await NodeSelected.InvokeAsync(Node.NodeId);
        await NodeEditRequested.InvokeAsync(Node.NodeId);
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
            builder.AddAttribute(24, nameof(Catalog), Catalog);
            builder.AddAttribute(25, nameof(ContentPolicy), ContentPolicy);
            builder.AddAttribute(26, nameof(RenderedFragmentKinds), RenderedFragmentKinds);
            builder.AddAttribute(27, nameof(MoveSourceNode), MoveSourceNode);
            builder.AddAttribute(28, nameof(CanAcceptMoveAsSibling), CanAcceptMoveInside());
            builder.AddAttribute(29, nameof(PreviewMode), PreviewMode);
            builder.AddAttribute(30, nameof(NodeSelected), NodeSelected);
            builder.AddAttribute(31, nameof(NodeEditRequested), NodeEditRequested);
            builder.CloseComponent();
        }
    }

    private static string FragmentKindToken(PageRenderedFragmentKind kind) => kind switch
    {
        PageRenderedFragmentKind.CustomHtml => "html",
        PageRenderedFragmentKind.Scriban => "scriban",
        PageRenderedFragmentKind.SharpTs => "typescript",
        PageRenderedFragmentKind.Htmx => "htmx",
        PageRenderedFragmentKind.Markdown => "markdown",
        _ => "rendered"
    };

    private sealed record FragmentPresentation(string Label, string Icon)
    {
        public static FragmentPresentation For(PageRenderedFragmentKind kind) => kind switch
        {
            PageRenderedFragmentKind.CustomHtml => new("Custom HTML", "</>"),
            PageRenderedFragmentKind.Scriban => new("Scriban", "{{ }}"),
            PageRenderedFragmentKind.SharpTs => new("TypeScript (SharpTS)", "TS"),
            PageRenderedFragmentKind.Htmx => new("HTMX", "hx"),
            PageRenderedFragmentKind.Markdown => new("Markdown", "M↓"),
            _ => new("Rendered content", "</>")
        };
    }

    private bool CanHaveChildren() =>
        Catalog.TryGet(Node.TagName, out var definition)
        && definition is not null
        && definition.ChildModel is not HtmlChildModel.None;

    private bool IsStructuralContainer() =>
        Catalog.TryGet(Node.TagName, out var definition)
        && definition is not null
        && definition.PaletteCategory.Equals("Structural", StringComparison.OrdinalIgnoreCase);

    private bool CanAcceptMoveInside() =>
        MoveSourceNode is not null
        && MoveSourceNode.NodeId != Node.NodeId
        && ContentPolicy.CanContain(Node, MoveSourceNode).IsAllowed;
}
