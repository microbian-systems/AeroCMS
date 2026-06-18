using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Canvas;

/// <summary>
/// Root-level wrapper for the page-editor composition tree.
/// Cascades <see cref="IPageEditorDefinitionRegistry"/> into the tree and
/// coordinates selection tracking across all rendered nodes.
///
/// Usage:
/// <code>
/// &lt;CanvasTree RootNode="@compositionRoot"
///              SelectedNodeId="@selectedNodeId"
///              OnNodeSelected="@HandleNodeSelected"
///              OnNodeEditRequested="@HandleNodeEdit" /&gt;
/// </code>
/// </summary>
public sealed partial class CanvasTree : ComponentBase
{
    [Parameter, EditorRequired]
    public NeoPageNode RootNode { get; set; } = default!;

    /// <summary>
    /// The identifier of the currently selected node in the tree.
    /// The tree uses this value to compute <c>IsSelected</c> for each rendered node.
    /// </summary>
    [Parameter]
    public string? SelectedNodeId { get; set; }

    /// <summary>
    /// Fired when a node in the tree is clicked (selected).
    /// Receives the <see cref="NeoPageNode.NodeId"/> of the clicked node.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeSelected { get; set; }

    /// <summary>
    /// Fired when a node in the tree is double-clicked (edit requested).
    /// Receives the <see cref="NeoPageNode.NodeId"/> of the double-clicked node.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeEditRequested { get; set; }

    /// <summary>
    /// Fired when an item is dropped anywhere in the composition tree.
    /// Receives the <see cref="CanvasDropArgs"/> describing the drop target.
    /// </summary>
    [Parameter]
    public EventCallback<CanvasDropArgs> OnDrop { get; set; }

    /// <summary>
    /// Fired when the user requests to copy a node in the tree.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnCopy { get; set; }

    /// <summary>
    /// Fired when the user requests to paste into a node in the tree.
    /// Receives the target node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnPaste { get; set; }

    /// <summary>
    /// Fired when the user requests to save a node as a custom component.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSaveAsCustom { get; set; }

    /// <summary>
    /// Fired when the user requests to duplicate a node in the tree.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnDuplicate { get; set; }

    /// <summary>
    /// Fired when the user requests to delete a node from the tree.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnDelete { get; set; }

    /// <summary>
    /// Fired when the user requests to move a node up within its parent.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnMoveUp { get; set; }

    /// <summary>
    /// Fired when the user requests to move a node down within its parent.
    /// Receives the node ID.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnMoveDown { get; set; }

    /// <summary>
    /// Optional cascading registry for resolving preview component types.
    /// When not provided, nodes fall back to displaying their catalog ID.
    /// </summary>
    [CascadingParameter]
    public IPageEditorDefinitionRegistry? DefinitionRegistry { get; set; }
}
