using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

public partial class ContentItemsList
{
    [Parameter] public string Alias { get; set; } = string.Empty;

    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private RadzenDataGrid<ContentItemSummary>? _grid;
    private IEnumerable<ContentItemSummary> _items = [];
    private int _count;
    private bool _isLoading = true;
    private string _searchText = string.Empty;
    private string _typeName = "Content";
    private bool _allowPublicUrl;

    private string HeaderDescription => _allowPublicUrl
        ? $"Managing {_count} {_typeName.ToLowerInvariant()} entries with optional public pages."
        : $"Managing {_count} {_typeName.ToLowerInvariant()} entries for embedding in pages and blocks.";

    protected override async Task OnInitializedAsync()
    {
        var result = await ContentTypesApi.GetByAliasAsync(Alias);
        if (result is Result<ContentTypeDetail, AeroError>.Ok ok)
        {
            _typeName = ok.Value.Name;
            _allowPublicUrl = ok.Value.AllowPublicUrl;
            return;
        }

        _typeName = Alias;
    }

    private async Task LoadData(LoadDataArgs args)
    {
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
