using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager;

public partial class Docs
{
    [Inject] private IDocsHttpClient DocsClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private RadzenDataGrid<DocsSpaceRow>? _grid;
    private IReadOnlyList<DocsSummary> _allDocs = [];
    private IReadOnlyList<DocsSpaceRow> _spaces = [];
    private string _searchText = string.Empty;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _showCreateForm;
    private SpaceDraft _newSpace = new();

    private IReadOnlyList<DocsSpaceRow> FilteredSpaces => string.IsNullOrWhiteSpace(_searchText)
        ? _spaces
        : _spaces
            .Where(space =>
                space.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                space.Slug.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (space.Summary?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
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
            var allResult = await DocsClient.GetAllAsync();
            if (allResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Ok allOk)
            {
                _allDocs = allOk.Value;
            }
            else if (allResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Failure allFailure)
            {
                NotifyError(L["Failed to load docs"], allFailure.Error.ToString());
                _allDocs = [];
            }

            var spacesResult = await DocsClient.GetCategoriesAsync();
            if (spacesResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Ok spacesOk)
            {
                _spaces = spacesOk.Value
                    .Select(space => new DocsSpaceRow(
                        space.Id,
                        space.Title,
                        space.Slug,
                        space.Summary,
                        _allDocs.Count(doc => doc.ParentId == space.Id),
                        space.PublicationState,
                        space.PublishedOn,
                        space.ModifiedOn))
                    .ToList();
            }
            else if (spacesResult is Result<IReadOnlyList<DocsSummary>, AeroError>.Failure spacesFailure)
            {
                NotifyError(L["Failed to load spaces"], spacesFailure.Error.ToString());
                _spaces = [];
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text;
        _grid?.Reload();
    }

    private void ShowCreateSpace()
    {
        _newSpace = new SpaceDraft();
        _showCreateForm = true;
    }

    private void CancelCreateSpace()
    {
        _showCreateForm = false;
        _newSpace = new SpaceDraft();
    }

    private void OnNewSpaceTitleChanged(ChangeEventArgs args)
    {
        _newSpace.Title = args.Value?.ToString() ?? string.Empty;
        if (!_newSpace.SlugLocked)
        {
            _newSpace.Slug = GenerateSlug(_newSpace.Title);
        }
    }

    private async Task CreateSpaceAsync()
    {
        if (string.IsNullOrWhiteSpace(_newSpace.Title) || string.IsNullOrWhiteSpace(_newSpace.Slug))
        {
            NotifyError(L["Missing fields"], L["Title and slug are required."]);
            return;
        }

        var root = _allDocs.FirstOrDefault(doc => string.Equals(doc.Slug, "docs", StringComparison.OrdinalIgnoreCase));
        if (root is null)
        {
            NotifyError(L["Missing docs root"], L["Create the virtual docs root before adding a space."]);
            return;
        }

        _isSaving = true;
        try
        {
            var detail = DocsDetail.Create(
                _newSpace.Title,
                NormalizeSlug(_newSpace.Slug),
                root.Id,
                _newSpace.Summary,
                ContentPublicationState.Draft);

            var result = await DocsClient.SaveAsync(detail);
            if (result is Result<DocsDetail, AeroError>.Ok ok)
            {
                NotificationService.Notify(NotificationSeverity.Success, L["Space created"], ok.Value.Title);
                _showCreateForm = false;
                await LoadAsync();
                Navigation.NavigateTo($"/manager/docs/{ok.Value.Id}");
                return;
            }

            if (result is Result<DocsDetail, AeroError>.Failure failure)
            {
                NotifyError(L["Create failed"], failure.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OnSpaceRowClick(DataGridRowMouseEventArgs<DocsSpaceRow> args)
    {
        OpenSpace(args.Data.Id);
    }

    private void OpenSpace(long spaceId)
    {
        Navigation.NavigateTo($"/manager/docs/{spaceId}");
    }

    private async Task TogglePublicationAsync(DocsSpaceRow row)
    {
        var nextState = row.PublicationState == ContentPublicationState.Published
            ? ContentPublicationState.Draft
            : ContentPublicationState.Published;

        var saveResult = nextState == ContentPublicationState.Published
            ? await DocsClient.PublishAsync(row.Id)
            : await DocsClient.UnpublishAsync(row.Id);

        if (saveResult is Result<DocsDetail, AeroError>.Ok)
        {
            NotificationService.Notify(NotificationSeverity.Success, L["Updated"], string.Format(L["{0} is now {1}."], row.Title, nextState));
            await LoadAsync();
            return;
        }

        if (saveResult is Result<DocsDetail, AeroError>.Failure saveFailure)
        {
            NotifyError(L["Update failed"], saveFailure.Error.ToString());
        }
    }

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("MMM d, yyyy") ?? "-";

    private void NotifyError(string summary, string detail)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = summary,
            Detail = detail,
            Duration = 5000
        });
    }

    private static string NormalizeSlug(string value)
        => GenerateSlug(value).Trim('/');

    private static string GenerateSlug(string value)
    {
        var slug = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private sealed record DocsSpaceRow(
        long Id,
        string Title,
        string Slug,
        string? Summary,
        int SectionCount,
        ContentPublicationState PublicationState,
        DateTimeOffset? PublishedOn,
        DateTimeOffset? ModifiedOn);

    private sealed class SpaceDraft
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public bool SlugLocked { get; set; }
    }
}
