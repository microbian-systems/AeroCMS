using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Shared.Blocks.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Renders a composition-capable node along with its children in the page-editor
/// composition tree. Extends the rendering behavior of <see cref="CanvasNode"/>
/// with a children area, drop-zone indicators, and recursive child rendering.
///
/// Resolves <see cref="IPageEditorCatalogDefinition.PreviewComponentType"/> via
/// the cascaded <see cref="IPageEditorDefinitionRegistry"/> for the node preview,
/// and uses the registry to determine whether each child can itself contain children
/// (switching between <see cref="CanvasContainer"/> and <see cref="CanvasNode"/>).
/// </summary>
public sealed partial class CanvasContainer : ComponentBase
{
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    [Parameter]
    public NeoPageNode? RootNode { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public int Depth { get; set; }

    /// <summary>
    /// The currently selected node ID in the composition tree.
    /// Used to compute <c>IsSelected</c> for child nodes rendered by this container.
    /// </summary>
    [Parameter]
    public string? SelectedNodeId { get; set; }

    [Parameter]
    public EventCallback<string> OnSelect { get; set; }

    [Parameter]
    public EventCallback<string> OnEdit { get; set; }

    [Parameter]
    public EventCallback<CompositionMutation> OnNodeChanged { get; set; }

    /// <summary>
    /// Fired when a nested composition surface rejects a drag/drop operation.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnDropRejected { get; set; }

    /// <summary>
    /// Fired when an item is dropped into one of this container's drop zones.
    /// </summary>
    [Parameter]
    public EventCallback<CanvasDropArgs> OnDrop { get; set; }

    /// <summary>Fired when the user requests a copy of this node.</summary>
    [Parameter]
    public EventCallback<string> OnCopy { get; set; }

    /// <summary>Fired when the user requests a paste into this node.</summary>
    [Parameter]
    public EventCallback<string> OnPaste { get; set; }

    /// <summary>Fired when the user requests to save this node as a custom component.</summary>
    [Parameter]
    public EventCallback<string> OnSaveAsCustom { get; set; }

    /// <summary>Fired when the user requests to duplicate this node.</summary>
    [Parameter]
    public EventCallback<string> OnDuplicate { get; set; }

    /// <summary>Fired when the user requests to delete this node.</summary>
    [Parameter]
    public EventCallback<string> OnDelete { get; set; }

    /// <summary>Fired when the user requests to move this node up in its parent.</summary>
    [Parameter]
    public EventCallback<string> OnMoveUp { get; set; }

    /// <summary>Fired when the user requests to move this node down in its parent.</summary>
    [Parameter]
    public EventCallback<string> OnMoveDown { get; set; }

    /// <summary>
    /// Zero-based index within the parent's children list.
    /// Used to compute move-up/move-down availability in the context menu.
    /// </summary>
    [Parameter]
    public int Index { get; set; }

    /// <summary>
    /// Total number of siblings in the parent's children list.
    /// Used to compute move-up/move-down availability in the context menu.
    /// </summary>
    [Parameter]
    public int TotalSiblings { get; set; }

    [CascadingParameter]
    public IPageEditorDefinitionRegistry? DefinitionRegistry { get; set; }

    [CascadingParameter]
    public IBlockEditorCallbacks? Editor { get; set; }

    /// <summary>
    /// Optional composition policy for drop validation. When not cascaded,
    /// validation is deferred to the parent orchestrator.
    /// </summary>
    [CascadingParameter]
    public ICompositionPolicy? CompositionPolicy { get; set; }

    [Inject]
    private IEditorNodeActionProvider ActionProvider { get; set; } = default!;

    private bool ContextMenuOpen { get; set; }
    private double ContextMenuX { get; set; }
    private double ContextMenuY { get; set; }
    private IReadOnlyList<EditorNodeAction> _contextMenuActions = Array.Empty<EditorNodeAction>();

    private Type? PreviewComponentType { get; set; }

    private Dictionary<string, object> PreviewParameters { get; set; } = [];

    private string NodeDisplayName { get; set; } = string.Empty;

    private string CssClass
    {
        get
        {
            var css = "canvas-node canvas-node--container";
            if (!IsRootNode) css += " pe-block-wrapper";
            if (IsRootNode) css += " canvas-node--root";
            if (IsSelected)
            {
                css += " canvas-node--selected";
                if (!IsRootNode) css += " selected";
            }
            css += $" canvas-node--depth-{Depth}";
            return css;
        }
    }

    private string IndentStyle => IsRootNode ? string.Empty : $"padding-left: {Math.Max(0, Depth - 1) * 16}px;";

    private bool IsRootNode =>
        string.Equals(Node.CatalogId, "page.root", StringComparison.OrdinalIgnoreCase) ||
        Node.Kind == NeoPageNodeKind.Page;

    private bool ShouldShowFallbackLabel =>
        Node.Children.Count == 0 &&
        !string.Equals(Node.CatalogId, "page.root", StringComparison.OrdinalIgnoreCase);

    private bool ShouldRenderGenericChildren =>
        IsRootNode || PreviewComponentType is null;

    protected override void OnParametersSet()
    {
        ResolvePreviewType();
        BuildPreviewParameters();
    }

    private void ResolvePreviewType()
    {
        PreviewComponentType = null;
        NodeDisplayName = Node.CatalogId;

        if (DefinitionRegistry is null)
            return;

        if (DefinitionRegistry.TryGetDescriptor(Node.CatalogId, out var descriptor))
        {
            PreviewComponentType = descriptor.Catalog.PreviewComponentType;
            NodeDisplayName = descriptor.Catalog.DisplayName;
        }
    }

    private void BuildPreviewParameters()
    {
        PreviewParameters = new Dictionary<string, object>
        {
            ["Node"] = Node,
            ["RootNode"] = RootNode ?? Node,
            ["NodeChanged"] = EventCallback.Factory.Create<CompositionMutation>(
                this,
                mutation => OnNodeChanged.InvokeAsync(mutation)),
            ["NodeEditRequested"] = EventCallback.Factory.Create<string>(
                this,
                nodeId => OnEdit.InvokeAsync(nodeId)),
            ["DropRejected"] = EventCallback.Factory.Create<string>(
                this,
                message => OnDropRejected.InvokeAsync(message))
        };
    }

    private bool TryMapLegacyBlock(out Aero.Cms.Abstractions.Blocks.BlockBase block)
    {
        block = default!;

        if (DefinitionRegistry is null ||
            !DefinitionRegistry.TryGetDescriptor(Node.CatalogId, out var descriptor) ||
            descriptor.LegacyDefinition is null)
        {
            return NeoPageNodeLegacyBlockMapper.TryMap(Node, out block);
        }

        var editorBlock = NeoPageNodeEditorBlockMapper.ToEditorBlock(Node);
        if (descriptor.LegacyDefinition.ToBlockBase(editorBlock) is not { } mapped)
        {
            return NeoPageNodeLegacyBlockMapper.TryMap(Node, out block);
        }

        block = mapped;
        return true;
    }

    /// <summary>
    /// Checks whether the given child node can itself contain children,
    /// based on the registered composition capabilities.
    /// </summary>
    private bool CanContainChildren(NeoPageNode child)
    {
        if (DefinitionRegistry is null)
            return false;

        return DefinitionRegistry.TryGetDescriptor(child.CatalogId, out var descriptor)
            && descriptor.Catalog.Composition.CanContainChildren;
    }

    private async Task HandleClickAsync()
    {
        if (IsRootNode)
            return;

        if (OnSelect.HasDelegate)
            await OnSelect.InvokeAsync(Node.NodeId);
    }

    private async Task HandleDoubleClickAsync()
    {
        if (IsRootNode)
            return;

        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync(Node.NodeId);
    }

    private async Task HandleDropWithArgsAsync(DragEventArgs e, NeoPageNode? targetChild, int insertAtIndex)
    {
        if (!OnDrop.HasDelegate) return;

        await OnDrop.InvokeAsync(new CanvasDropArgs(
            Node.NodeId,
            targetChild?.NodeId,
            insertAtIndex));
    }

    private async Task OpenContextMenu(MouseEventArgs args)
    {
        if (IsRootNode)
            return;

        ContextMenuX = args.ClientX;
        ContextMenuY = args.ClientY;
        _contextMenuActions = ComputeAvailableActions();
        ContextMenuOpen = true;
        if (OnSelect.HasDelegate)
            await OnSelect.InvokeAsync(Node.NodeId);
    }

    private IReadOnlyList<EditorNodeAction> ComputeAvailableActions()
    {
        if (DefinitionRegistry is null)
            return Array.Empty<EditorNodeAction>();

        if (!DefinitionRegistry.TryGetDescriptor(Node.CatalogId, out var descriptor))
            return Array.Empty<EditorNodeAction>();

        var interaction = descriptor.Interaction;
        var context = new EditorNodeActionContext(
            HasClipboardContent: false,
            CanMoveUp: Index > 0,
            CanMoveDown: Index < TotalSiblings - 1,
            CanSaveAsCustom: interaction.HasFlag(EditorInteractionCapabilities.Editable));

        return ActionProvider.GetAvailableActions(interaction, context);
    }

    private async Task ExecuteContextMenuAction(EditorNodeAction action)
    {
        ContextMenuOpen = false;

        switch (action)
        {
            case EditorNodeAction.Edit:
                if (OnEdit.HasDelegate)
                    await OnEdit.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.Duplicate:
                if (OnDuplicate.HasDelegate)
                    await OnDuplicate.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.Delete:
                if (OnDelete.HasDelegate)
                    await OnDelete.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.Copy:
                if (OnCopy.HasDelegate)
                    await OnCopy.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.Paste:
                if (OnPaste.HasDelegate)
                    await OnPaste.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.MoveUp:
                if (OnMoveUp.HasDelegate)
                    await OnMoveUp.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.MoveDown:
                if (OnMoveDown.HasDelegate)
                    await OnMoveDown.InvokeAsync(Node.NodeId);
                break;
            case EditorNodeAction.SaveAsCustom:
                if (OnSaveAsCustom.HasDelegate)
                    await OnSaveAsCustom.InvokeAsync(Node.NodeId);
                break;
            // MediaSelect is intentionally skipped — no canvas-level handler yet
        }
    }

    private string GetActionDisplayText(EditorNodeAction action) => action switch
    {
        EditorNodeAction.Edit => L["Edit"],
        EditorNodeAction.Delete => L["Delete"],
        EditorNodeAction.Duplicate => L["Duplicate"],
        EditorNodeAction.Copy => L["Copy"],
        EditorNodeAction.Paste => L["Paste"],
        EditorNodeAction.MoveUp => L["MoveUp"],
        EditorNodeAction.MoveDown => L["MoveDown"],
        EditorNodeAction.SaveAsCustom => L["SaveAsCustom"],
        EditorNodeAction.MediaSelect => L["MediaSelect"],
        _ => action.ToString()
    };
}
