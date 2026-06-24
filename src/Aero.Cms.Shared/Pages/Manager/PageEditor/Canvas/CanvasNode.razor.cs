using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Styles;
using Aero.Cms.Shared.Blocks.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Renders a single node in the page-editor composition tree.
/// Resolves the node's <see cref="IPageEditorCatalogDefinition.PreviewComponentType"/>
/// via the cascaded <see cref="IPageEditorDefinitionRegistry"/> and renders it as a
/// <see cref="DynamicComponent"/> with <c>Node</c> and <c>Breakpoint</c> parameters.
/// When no preview component is registered, falls back to displaying the raw
/// <see cref="NeoPageNode.CatalogId"/> only for unknown leaf nodes.
/// </summary>
public sealed partial class CanvasNode : ComponentBase
{
    [Parameter, EditorRequired]
    public NeoPageNode Node { get; set; } = default!;

    [Parameter]
    public NeoPageNode? RootNode { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public int Depth { get; set; }

    [Parameter]
    public EventCallback<string> OnSelect { get; set; }

    [Parameter]
    public EventCallback<string> OnEdit { get; set; }

    [Parameter]
    public EventCallback<CompositionMutation> OnNodeChanged { get; set; }

    /// <summary>
    /// Fired when a nested composition preview surface rejects a drag/drop operation.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnDropRejected { get; set; }

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
            var css = "canvas-node pe-block-wrapper";
            if (IsSelected) css += " canvas-node--selected selected";
            css += $" canvas-node--depth-{Depth}";
            return css;
        }
    }

    private string IndentStyle => $"padding-left: {Math.Max(0, Depth - 1) * 16}px;";

    private bool ShouldShowFallbackLabel =>
        Node.Children.Count == 0 &&
        !string.Equals(Node.CatalogId, "page.root", StringComparison.OrdinalIgnoreCase);

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
            ["Node"] = Node
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

    private async Task HandleClickAsync()
    {
        if (OnSelect.HasDelegate)
            await OnSelect.InvokeAsync(Node.NodeId);
    }

    private async Task HandleDoubleClickAsync()
    {
        if (OnEdit.HasDelegate)
            await OnEdit.InvokeAsync(Node.NodeId);
    }

    private async Task OpenContextMenu(MouseEventArgs args)
    {
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
