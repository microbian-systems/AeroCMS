using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Represents a class for ContentItemsList.
/// </summary>
public partial class ContentItemsList
{
        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
[Parameter] public string Alias { get; set; } = string.Empty;

    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<ContentItemSummary>? _grid;
    private IEnumerable<ContentItemSummary> _items = [];
    private int _count;
    private bool _isLoading = false;
    private bool _hasRequestedInitialItems;
    private string _searchText = string.Empty;
    private string _typeName = "Content";
    private bool _allowPublicUrl;
    private bool _isHierarchical;

    private string HeaderDescription => _isHierarchical
        ? L["Browse, nest, and reorder {0} entries.", _typeName.ToLowerInvariant()]
        : _allowPublicUrl
            ? L["Managing {0} {1} entries with optional public pages.", _count, _typeName.ToLowerInvariant()]
            : L["Managing {0} {1} entries for embedding in pages and blocks.", _count, _typeName.ToLowerInvariant()];

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        var result = await ContentTypesApi.GetByAliasAsync(Alias);
        if (result is Result<ContentTypeDetail, AeroError>.Ok ok)
        {
            _typeName = ok.Value.Name;
            _allowPublicUrl = ok.Value.AllowPublicUrl;
            _isHierarchical = ok.Value.Structure == Aero.Cms.Abstractions.Content.ContentStructure.Hierarchical;
        }
        else
        {
            _typeName = Alias;
        }

        if (!_isHierarchical && !_hasRequestedInitialItems)
        {
            _hasRequestedInitialItems = true;
            await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
        }
    }

    private async Task LoadData(LoadDataArgs args)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var result = await ContentItemsApi.GetAllAsync(Alias, args.Skip ?? 0, args.Top ?? 10, _searchText);
            if (result is Result<PagedResult<ContentItemSummary>, AeroError>.Ok ok)
            {
                _items = ok.Value.Items;
                _count = (int)ok.Value.TotalCount;
                return;
            }

            if (result is Result<PagedResult<ContentItemSummary>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Load failed", failure.Error.ToString());
            }

            _items = [];
            _count = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnSearchChanged(string value)
    {
        _searchText = value;
        if (_grid is not null)
        {
            await _grid.FirstPage();
        }
    }

    private void CreateItem()
        => Navigation.NavigateTo($"/manager/content/{Alias}/editor");

    private void EditItem(long id)
        => Navigation.NavigateTo($"/manager/content/{Alias}/editor/{id}");

    private void OnRowClick(DataGridRowMouseEventArgs<ContentItemSummary> args)
        => EditItem(args.Data.Id);

    private bool CanOpenPublishedPage(ContentItemSummary item)
        => _allowPublicUrl &&
           string.Equals(item.PublicationState, "Published", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(Alias) &&
           !string.IsNullOrWhiteSpace(item.Slug);

    private string BuildPublicContentPath(string slug)
        => $"/content/{Uri.EscapeDataString(Alias.Trim())}/{Uri.EscapeDataString(slug.Trim())}";

    private string BuildPublicContentUrl(string slug)
        => new Uri(new Uri(Navigation.BaseUri), BuildPublicContentPath(slug).TrimStart('/')).ToString();

    private async Task DeleteItemAsync(long id)
    {
        var confirmed = await DialogService.Confirm(
            "Delete this entry? This cannot be undone.",
            "Delete Entry",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true) return;

        var result = await ContentItemsApi.DeleteAsync(Alias, id);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Deleted", "Entry removed.");
        if (_grid is not null)
        {
            await _grid.Reload();
        }
    }

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("MMM d, yyyy") ?? "-";

    private static string FormatFirstField(string value)
    {
        var trimmed = value.Trim('"');
        return trimmed.Length <= 96 ? trimmed : $"{trimmed[..96]}...";
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });
}
