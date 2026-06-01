using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

public partial class ContentTypeList
{
    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<ContentTypeSummary>? _grid;
    private IReadOnlyList<ContentTypeSummary> _types = [];
    private bool _isLoading = true;
    private string _searchText = string.Empty;

    private IReadOnlyList<ContentTypeSummary> FilteredTypes => string.IsNullOrWhiteSpace(_searchText)
        ? _types
        : _types
            .Where(type =>
                type.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                type.Alias.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (type.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (type.Category?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        try
        {
            var result = await ContentTypesApi.GetAllAsync();
            if (result is Result<IReadOnlyList<ContentTypeSummary>, AeroError>.Ok ok)
            {
                _types = ok.Value;
                return;
            }

            if (result is Result<IReadOnlyList<ContentTypeSummary>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Load failed", failure.Error.ToString());
            }

            _types = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnSearchChanged(string value)
    {
        _searchText = value;
        _grid?.Reload();
    }

    private void CreateType()
        => Navigation.NavigateTo("/manager/content-type/editor");

    private void EditType(string alias)
        => Navigation.NavigateTo($"/manager/content-type/editor/{Uri.EscapeDataString(alias)}");

    private void OnRowClick(DataGridRowMouseEventArgs<ContentTypeSummary> args)
        => EditType(args.Data.Alias);

    private async Task DeleteTypeAsync(string alias)
    {
        var confirmed = await DialogService.Confirm(
            $"Delete content type '{alias}'? Existing entries should be removed first.",
            "Delete Content Type",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true) return;

        var result = await ContentTypesApi.DeleteAsync(alias);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Deleted", "Content type removed.");
        await LoadAsync();
        _grid?.Reload();
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
