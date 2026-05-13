using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

public sealed record AddNavMenuLinkDialogResult(string Label, string Url, long? PageId, string? AltText);

public partial class AddNavMenuLinkDialog
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private ILogger<AddNavMenuLinkDialog> Logger { get; set; } = default!;

    private string _label = string.Empty;
    private string _url = "/";
    private string? _altText;
    private string? _pageSearch;
    private long? _selectedPageId;
    private IReadOnlyList<PageSummary> _pages = Array.Empty<PageSummary>();
    private bool _showPageSearch;
    private bool _isSearching;

    private bool IsSubmitDisabled => string.IsNullOrWhiteSpace(_label) || string.IsNullOrWhiteSpace(_url);

    protected override async Task OnInitializedAsync()
    {
        await SearchPagesAsync();
    }

    private void TogglePageSearch()
    {
        _showPageSearch = !_showPageSearch;
    }

    private void OnUrlChanged(string? value)
    {
        _url = value ?? string.Empty;
        _selectedPageId = null;
    }

    private void OnPageSearchChanged(string? value)
    {
        _pageSearch = value;
    }

    private async Task SearchPagesAsync()
    {
        _isSearching = true;
        try
        {
            var result = await PagesClient.GetAllAsync(take: 10, search: _pageSearch);
            if (result is Result<PagedResult<PageSummary>, AeroError>.Ok ok)
            {
                _pages = ok.Value.Items;
            }
            else if (result is Result<PagedResult<PageSummary>, AeroError>.Failure fail)
            {
                _pages = Array.Empty<PageSummary>();
                Logger.LogWarning("Failed to search pages for nav link. Error: {Error}", fail.Error);
            }
        }
        catch (Exception ex)
        {
            _pages = Array.Empty<PageSummary>();
            Logger.LogError(ex, "Failed to search pages for nav link");
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task SelectPageAsync(PageSummary? page)
    {
        if (page is null)
        {
            return;
        }

        var result = await PagesClient.GetByIdAsync(page.Id);
        if (result is Result<PageDetail, AeroError>.Ok ok)
        {
            var detail = ok.Value;
            _label = string.IsNullOrWhiteSpace(_label) ? detail.Title : _label;
            _url = NormalizePageUrl(detail);
            _selectedPageId = detail.Id;
            _showPageSearch = false;
            return;
        }

        _label = string.IsNullOrWhiteSpace(_label) ? page.Title : _label;
        _url = NormalizeSlug(page.Slug);
        _selectedPageId = page.Id;
        _showPageSearch = false;
    }

    private void Submit()
    {
        if (IsSubmitDisabled)
        {
            return;
        }

        DialogService.Close(new AddNavMenuLinkDialogResult(
            _label.Trim(),
            _url.Trim(),
            _selectedPageId,
            _altText?.Trim()));
    }

    private void Cancel()
    {
        DialogService.Close(null);
    }

    private static string NormalizePageUrl(PageDetail page)
    {
        if (!string.IsNullOrWhiteSpace(page.Path))
        {
            return NormalizeSlug(page.Path);
        }

        return NormalizeSlug(page.Slug);
    }

    private static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug == "/")
        {
            return "/";
        }

        return $"/{slug.Trim().TrimStart('/')}";
    }
}
