using Aero.Cms.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public partial class HtmlPageEditorCanvas : IAsyncDisposable
{
    private ElementReference _surface;
    private ElementReference _dragHandle;
    private HtmlSortableInterop? _sortable;
    private DotNetObjectReference<HtmlPageEditorCanvas>? _callbackReference;
    private HtmlNode? _paletteMoveSource;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public HtmlPageContent Content { get; set; } = new();

    [Parameter, EditorRequired]
    public HtmlElementCatalog Catalog { get; set; } = null!;

    [Parameter, EditorRequired]
    public IHtmlContentModelPolicy ContentPolicy { get; set; } = null!;

    [Parameter]
    public long? SelectedNodeId { get; set; }

    [Parameter]
    public CompiledPageStyles? CompiledStyles { get; set; }

    [Parameter]
    public bool PreviewMode { get; set; }

    [Parameter]
    public EventCallback<long?> SelectionChanged { get; set; }

    [Parameter]
    public EventCallback<string> ElementRequested { get; set; }

    [Parameter]
    public EventCallback<long> NodeEditRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlSortMoveIntent> SortMoveRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlPaletteInsertIntent> PaletteInsertRequested { get; set; }

    [Parameter]
    public EventCallback<HtmlEditorCommandKind> EditorCommandRequested { get; set; }

    private string RootNodeId => Content.Root.NodeId.ToString(CultureInfo.InvariantCulture);

    private HtmlNode? SelectedMoveSource => SelectedNodeId is { } nodeId
        ? HtmlTreeOperations.FindById(Content.Root, nodeId)
        : null;

    private HtmlNode? ActiveMoveSource => _paletteMoveSource ?? SelectedMoveSource;

    private bool CanRootAcceptMove => ActiveMoveSource is { } source
        && ContentPolicy.CanContain(Content.Root, source).IsAllowed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _sortable = new HtmlSortableInterop(JS);
        _callbackReference = DotNetObjectReference.Create(this);
        await _sortable.InitializeAsync(_surface, _dragHandle, _callbackReference);
    }

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

    [JSInvokable]
    public Task OnPaletteDragEnded()
    {
        _paletteMoveSource = null;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task OnEditorCommandRequested(string command)
    {
        if (PreviewMode || !Enum.TryParse<HtmlEditorCommandKind>(command, true, out var parsedCommand))
        {
            return Task.CompletedTask;
        }

        return EditorCommandRequested.InvokeAsync(parsedCommand);
    }

    private Task SelectNodeAsync(long nodeId) => SelectionChanged.InvokeAsync(nodeId);

    private Task ClearSelectionAsync() => PreviewMode
        ? Task.CompletedTask
        : SelectionChanged.InvokeAsync(null);

    private Task RequestFirstSectionAsync() => ElementRequested.InvokeAsync("section");

    private Task EditNodeAsync(long nodeId) => NodeEditRequested.InvokeAsync(nodeId);

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
