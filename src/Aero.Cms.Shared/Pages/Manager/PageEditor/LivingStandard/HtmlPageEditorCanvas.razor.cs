using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Renders the editable HTML tree and translates browser drag, drop, selection, and command
/// events into strongly typed editor callbacks.
/// </summary>
/// <remarks>
/// JavaScript reports user intent only. This component parses identifiers and enum tokens, while
/// the owning editor remains authoritative for validating and applying tree mutations.
/// </remarks>
public partial class HtmlPageEditorCanvas : IAsyncDisposable
{
    private ElementReference _surface;
    private ElementReference _selectionToolbar;
    private ElementReference _dragHandle;
    private HtmlSortableInterop? _sortable;
    private DotNetObjectReference<HtmlPageEditorCanvas>? _callbackReference;
    private HtmlNode? _paletteMoveSource;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>
    /// Gets or sets the page content rendered by the canvas.
    /// </summary>
    [Parameter, EditorRequired]
    public HtmlPageContent Content { get; set; } = new();

    /// <summary>
    /// Gets or sets the catalog used to create representative drag nodes for element requests.
    /// </summary>
    [Parameter, EditorRequired]
    public HtmlElementCatalog Catalog { get; set; } = null!;

    /// <summary>
    /// Gets or sets the policy used to preview whether the root can contain the active drag item.
    /// </summary>
    /// <remarks>The owner must validate the final insertion again before mutating the tree.</remarks>
    [Parameter, EditorRequired]
    public IHtmlContentModelPolicy ContentPolicy { get; set; } = null!;

    /// <summary>
    /// Gets or sets the node highlighted as the current selection.
    /// </summary>
    [Parameter]
    public long? SelectedNodeId { get; set; }

    /// <summary>
    /// Gets or sets the compiled styles applied to the preview surface.
    /// </summary>
    [Parameter]
    public CompiledPageStyles? CompiledStyles { get; set; }

    /// <summary>
    /// Gets or sets whether editing interactions are suppressed for preview-only rendering.
    /// </summary>
    [Parameter]
    public bool PreviewMode { get; set; }

    /// <summary>
    /// Gets or sets whether the selected node can move before its preceding sibling.
    /// </summary>
    [Parameter]
    public bool CanMoveSelectedUp { get; set; }

    /// <summary>
    /// Gets or sets whether the selected node can move after its following sibling.
    /// </summary>
    [Parameter]
    public bool CanMoveSelectedDown { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the selected node changes.
    /// </summary>
    [Parameter]
    public EventCallback<long?> SelectionChanged { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests insertion of a catalog element by tag name.
    /// </summary>
    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that requests rich editing for a node identifier.
    /// </summary>
    [Parameter]
    public EventCallback<long> NodeEditRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives a parsed tree-move intent.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlSortMoveIntent> SortMoveRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives a parsed palette insertion intent.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlPaletteInsertIntent> PaletteInsertRequested { get; set; }

    /// <summary>
    /// Gets or sets the callback that receives a parsed canvas command.
    /// </summary>
    [Parameter]
    public EventCallback<HtmlEditorCommandKind> EditorCommandRequested { get; set; }

    private string RootNodeId => Content.Root.NodeId.ToString(CultureInfo.InvariantCulture);

    private HtmlNode? SelectedMoveSource => SelectedNodeId is { } nodeId
        ? HtmlTreeOperations.FindById(Content.Root, nodeId)
        : null;

    private HtmlNode? ActiveMoveSource => _paletteMoveSource ?? SelectedMoveSource;

    private bool CanRootAcceptMove => ActiveMoveSource is { } source
        && ContentPolicy.CanContain(Content.Root, source).IsAllowed;

    /// <summary>
    /// Initializes the browser-side sortable integration after the canvas first renders.
    /// </summary>
    /// <param name="firstRender">
    /// <see langword="true"/> for the first completed render; otherwise,
    /// <see langword="false"/>.
    /// </param>
    /// <returns>A task that completes after sortable initialization, when required.</returns>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _sortable = new HtmlSortableInterop(JS);
        _callbackReference = DotNetObjectReference.Create(this);
        await _sortable.InitializeAsync(_surface, _selectionToolbar, _dragHandle, _callbackReference);
    }

    /// <summary>
    /// Parses a browser-reported node move and forwards valid edit-mode intent to the owner.
    /// </summary>
    /// <param name="nodeId">The invariant-culture identifier of the node being moved.</param>
    /// <param name="targetNodeId">The invariant-culture identifier of the relative target.</param>
    /// <param name="placement">The case-insensitive relative-placement enum name.</param>
    /// <returns>
    /// A task for the owner callback, or a completed task when preview mode is active or any
    /// browser value is invalid.
    /// </returns>
    [JSInvokable]
    public Task OnSortMoveRequested(
        string nodeId,
        string targetNodeId,
        string placement)
    {
        if (PreviewMode
            || !long.TryParse(nodeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNodeId)
            || !long.TryParse(targetNodeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTargetNodeId)
            || !Enum.TryParse<HtmlRelativePlacement>(placement, true, out var parsedPlacement))
        {
            return Task.CompletedTask;
        }

        return SortMoveRequested.InvokeAsync(
            new HtmlSortMoveIntent(parsedNodeId, parsedTargetNodeId, parsedPlacement));
    }

    /// <summary>
    /// Starts palette-drag feedback by creating a representative node for containment checks.
    /// </summary>
    /// <param name="itemKind">The case-insensitive palette item kind reported by JavaScript.</param>
    /// <param name="itemValue">
    /// The element tag or enum token identifying the dragged palette item.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a representative drag source was created; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// The representative node is transient and is never inserted into <see cref="Content"/>.
    /// </remarks>
    [JSInvokable]
    public async Task<bool> OnPaletteDragStarted(string itemKind, string itemValue)
    {
        if (PreviewMode
            || !Enum.TryParse<HtmlPaletteItemKind>(itemKind, true, out var parsedKind))
        {
            return false;
        }

        _paletteMoveSource = parsedKind switch
        {
            HtmlPaletteItemKind.Element when Catalog.TryGet(itemValue, out _) => Catalog.CreateElement(itemValue),
            HtmlPaletteItemKind.Layout when Enum.TryParse<HtmlLayoutStarterKind>(itemValue, true, out _)
                => Catalog.CreateElement("section"),
            HtmlPaletteItemKind.Component when Enum.TryParse<HtmlComponentTemplateKind>(itemValue, true, out _)
                => Catalog.CreateElement("section"),
            _ => null
        };

        if (_paletteMoveSource is null)
        {
            return false;
        }

        await InvokeAsync(StateHasChanged);
        return true;
    }

    /// <summary>
    /// Ends drag feedback and forwards a valid palette insertion intent to the owning editor.
    /// </summary>
    /// <param name="itemKind">The case-insensitive palette item kind.</param>
    /// <param name="itemValue">The tag or enum token identifying the requested item.</param>
    /// <param name="targetNodeId">The invariant-culture identifier of the relative target.</param>
    /// <param name="placement">The case-insensitive relative-placement enum name.</param>
    /// <returns>
    /// A task for the owner callback, or a render task when preview mode is active or any browser
    /// value is invalid.
    /// </returns>
    [JSInvokable]
    public Task OnPaletteInsertRequested(
        string itemKind,
        string itemValue,
        string targetNodeId,
        string placement)
    {
        _paletteMoveSource = null;
        if (PreviewMode
            || !Enum.TryParse<HtmlPaletteItemKind>(itemKind, true, out var parsedKind)
            || !long.TryParse(targetNodeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTargetNodeId)
            || !Enum.TryParse<HtmlRelativePlacement>(placement, true, out var parsedPlacement))
        {
            return InvokeAsync(StateHasChanged);
        }

        return PaletteInsertRequested.InvokeAsync(
            new HtmlPaletteInsertIntent(parsedKind, itemValue, parsedTargetNodeId, parsedPlacement));
    }

    /// <summary>
    /// Clears the transient palette drag source and refreshes containment feedback.
    /// </summary>
    /// <returns>A task that completes after the component refreshes.</returns>
    [JSInvokable]
    public Task OnPaletteDragEnded()
    {
        _paletteMoveSource = null;
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Parses and forwards an edit command reported by the browser integration.
    /// </summary>
    /// <param name="command">The case-insensitive editor-command enum name.</param>
    /// <returns>A task that completes after the owner handles the command and the canvas refreshes.</returns>
    /// <remarks>Preview mode and unknown commands are ignored.</remarks>
    [JSInvokable]
    public async Task OnEditorCommandRequested(string command)
    {
        if (PreviewMode || !Enum.TryParse<HtmlEditorCommandKind>(command, true, out var parsedCommand))
        {
            return;
        }

        await EditorCommandRequested.InvokeAsync(parsedCommand);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Requests selection of a node.
    /// </summary>
    /// <param name="nodeId">The node identifier to select.</param>
    /// <returns>A task that completes when the selection callback has finished.</returns>
    private Task SelectNodeAsync(long nodeId) => SelectionChanged.InvokeAsync(nodeId);

    /// <summary>
    /// Clears the current selection when the canvas is editable.
    /// </summary>
    /// <returns>A task that completes when the selection callback has finished.</returns>
    private Task ClearSelectionAsync() => PreviewMode
        ? Task.CompletedTask
        : SelectionChanged.InvokeAsync(null);

    /// <summary>
    /// Requests the default section used to start an empty page.
    /// </summary>
    /// <returns>A task that completes when the element callback has finished.</returns>
    private Task RequestFirstSectionAsync() => ElementRequested.InvokeAsync("section");

    /// <summary>
    /// Requests rich editing for the selected node.
    /// </summary>
    /// <param name="nodeId">The node identifier to edit.</param>
    /// <returns>A task that completes when the edit callback has finished.</returns>
    private Task EditNodeAsync(long nodeId) => NodeEditRequested.InvokeAsync(nodeId);

    /// <summary>
    /// Forwards a selected-element toolbar command to the owning editor.
    /// </summary>
    /// <param name="command">The requested editor command.</param>
    /// <returns>A task that completes after the owner handles the command.</returns>
    private Task RequestEditorCommandAsync(HtmlEditorCommandKind command) => PreviewMode
        ? Task.CompletedTask
        : EditorCommandRequested.InvokeAsync(command);

    /// <summary>
    /// Releases the sortable JavaScript integration and its .NET callback reference.
    /// </summary>
    /// <returns>A value task that completes after owned interop resources are released.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_sortable is not null)
            {
                await _sortable.DisposeAsync();
            }
        }
        finally
        {
            _callbackReference?.Dispose();
        }
    }
}
