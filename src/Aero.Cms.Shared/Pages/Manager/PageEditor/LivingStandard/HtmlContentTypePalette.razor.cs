using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>
/// Displays content-type metadata supplied by the page editor orchestrator.
/// </summary>
public partial class HtmlContentTypePalette
{
    private string _lastItemSearchText = string.Empty;

    /// <summary>Gets or sets the available content types.</summary>
    [Parameter]
    public IReadOnlyList<ContentTypeSummary> ContentTypes { get; set; } = [];

    /// <summary>Gets or sets the selected content-type alias.</summary>
    [Parameter]
    public string? SelectedAlias { get; set; }

    /// <summary>Gets or sets the selected content-type definition.</summary>
    [Parameter]
    public ContentTypeDetail? SelectedContentType { get; set; }

    /// <summary>Gets or sets the selectable items for the current content type.</summary>
    [Parameter]
    public IReadOnlyList<ContentItemSummary> ContentItems { get; set; } = [];

    /// <summary>Gets or sets the stable content item selected for an item scope.</summary>
    [Parameter]
    public long? SelectedContentItemId { get; set; }

    /// <summary>Gets or sets the applied content-item search text.</summary>
    [Parameter]
    public string ItemSearchText { get; set; } = string.Empty;

    /// <summary>Gets or sets the current content-item page offset.</summary>
    [Parameter]
    public int ContentItemSkip { get; set; }

    /// <summary>Gets or sets the current content-item page size.</summary>
    [Parameter]
    public int ContentItemTake { get; set; } = 10;

    /// <summary>Gets or sets the total number of matching content items.</summary>
    [Parameter]
    public long ContentItemTotalCount { get; set; }

    /// <summary>Gets or sets whether content metadata is loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Gets or sets whether selectable content items are loading.</summary>
    [Parameter]
    public bool IsItemsLoading { get; set; }

    /// <summary>Gets or sets a content loading error.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Raised when the selected content type changes.</summary>
    [Parameter]
    public EventCallback<string?> SelectedAliasChanged { get; set; }

    /// <summary>Raised when the selected stable content item changes.</summary>
    [Parameter]
    public EventCallback<long?> SelectedContentItemIdChanged { get; set; }

    /// <summary>Raised when the author applies a content-item search.</summary>
    [Parameter]
    public EventCallback<string> ItemSearchRequested { get; set; }

    /// <summary>Raised when the author requests the previous item page.</summary>
    [Parameter]
    public EventCallback PreviousItemsRequested { get; set; }

    /// <summary>Raised when the author requests the next item page.</summary>
    [Parameter]
    public EventCallback NextItemsRequested { get; set; }

    /// <summary>Raised when the author requests fresh content-type metadata.</summary>
    [Parameter]
    public EventCallback RefreshRequested { get; set; }

    /// <summary>Raised when a structured-content palette item is clicked.</summary>
    [Parameter]
    public EventCallback<HtmlContentPaletteRequest> ItemRequested { get; set; }

    private ContentItemSummary? SelectedContentItem => SelectedContentItemId is { } itemId
        ? ContentItems.FirstOrDefault(item => item.Id == itemId)
        : null;

    protected string PendingItemSearchText { get; private set; } = string.Empty;

    protected bool CanLoadPreviousItems => ContentItemSkip > 0;

    protected bool CanLoadNextItems => ContentItemSkip + ContentItemTake < ContentItemTotalCount;

    protected int TotalItemPages => ContentItemTake > 0
        ? Math.Max(1, (int)Math.Ceiling((double)ContentItemTotalCount / ContentItemTake))
        : 1;

    protected int CurrentItemPage => ContentItemTake > 0
        ? Math.Min(TotalItemPages, ContentItemSkip / ContentItemTake + 1)
        : 1;

    protected override void OnParametersSet()
    {
        if (!string.Equals(_lastItemSearchText, ItemSearchText, StringComparison.Ordinal))
        {
            _lastItemSearchText = ItemSearchText;
            PendingItemSearchText = ItemSearchText;
        }
    }

    private Task OnTypeChangedAsync(ChangeEventArgs args) =>
        SelectedAliasChanged.InvokeAsync(args.Value?.ToString());

    private Task OnItemChangedAsync(ChangeEventArgs args)
    {
        long? itemId = long.TryParse(
            args.Value?.ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedItemId)
                ? parsedItemId
                : null;
        return SelectedContentItemIdChanged.InvokeAsync(itemId);
    }

    private Task RefreshAsync() => RefreshRequested.InvokeAsync();

    protected void OnItemSearchInput(ChangeEventArgs args) =>
        PendingItemSearchText = args.Value?.ToString() ?? string.Empty;

    protected Task SearchItemsAsync() => ItemSearchRequested.InvokeAsync(PendingItemSearchText);

    protected Task LoadPreviousItemsAsync() => PreviousItemsRequested.InvokeAsync();

    protected Task LoadNextItemsAsync() => NextItemsRequested.InvokeAsync();

    private Task RequestAsync(HtmlContentPaletteRequest request) => ItemRequested.InvokeAsync(request);

    private static HtmlContentPaletteRequest CreateListRequest(ContentTypeDetail contentType) => new()
    {
        ItemKind = HtmlPaletteItemKind.ContentList,
        ContentTypeId = contentType.Id,
        ContentTypeAlias = contentType.Alias
    };

    private static HtmlContentPaletteRequest CreateItemRequest(
        ContentTypeDetail contentType,
        ContentItemSummary contentItem) => new()
    {
        ItemKind = HtmlPaletteItemKind.ContentItem,
        ContentTypeId = contentType.Id,
        ContentTypeAlias = contentType.Alias,
        ContentItemId = contentItem.Id,
        ContentItemSlug = contentItem.Slug,
        ContentItemTitle = contentItem.Title
    };

    private static HtmlContentPaletteRequest CreateFieldRequest(
        ContentTypeDetail contentType,
        ContentFieldDefinition field) => new()
    {
        ItemKind = HtmlPaletteItemKind.ContentField,
        ContentTypeId = contentType.Id,
        ContentTypeAlias = contentType.Alias,
        FieldName = field.Name,
        FieldType = field.FieldType,
        FieldLabel = field.Label
    };
}
