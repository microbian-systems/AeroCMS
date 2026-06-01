using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager;

public sealed record AddNavMenuLinkDialogResult(
    string Label,
    string Url,
    long? PageId,
    string? AltText,
    bool IsExternal,
    string Target);

public partial class AddNavMenuLinkDialog
{
    [Inject] private IPagesHttpClient PagesClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private ILogger<AddNavMenuLinkDialog> Logger { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private string _label = string.Empty;
    private string _url = "/";
    private string? _altText;
    private string? _pageSearch;
    private long? _selectedPageId;
    private bool _isExternal;
    private string _target = "_self";
    private IReadOnlyList<PageSummary> _pages = Array.Empty<PageSummary>();
    private bool _showPageSearch;
    private bool _isSearching;
    private IReadOnlyList<LinkTargetOption> TargetOptions =>
    [
        new("_self", L["Same tab"]),
        new("_blank", L["New tab"]),
        new("_parent", L["Parent frame"]),
        new("_top", L["Top frame"])
    ];

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

    private void OnExternalChanged(ChangeEventArgs args)
    {
        _isExternal = args.Value is bool value ? value : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
        if (_isExternal)
        {
            _selectedPageId = null;
            _showPageSearch = false;
            _target = "_blank";
            return;
        }

        _target = "_self";
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
            _isExternal = false;
            _target = "_self";
            _showPageSearch = false;
            return;
        }

        _label = string.IsNullOrWhiteSpace(_label) ? page.Title : _label;
        _url = NormalizeSlug(page.Slug);
        _selectedPageId = page.Id;
        _isExternal = false;
        _target = "_self";
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
            NormalizeSubmittedUrl(_url, _isExternal),
            _isExternal ? null : _selectedPageId,
            _altText?.Trim(),
            _isExternal,
            NormalizeTarget(_target, _isExternal)));
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

    private static string NormalizeSubmittedUrl(string value, bool isExternal)
    {
        var url = value.Trim();
        if (!isExternal || string.IsNullOrWhiteSpace(url) || url.Contains("://", StringComparison.Ordinal))
        {
            return url;
        }

        return $"https://{url}";
    }

    private static string NormalizeTarget(string? target, bool isExternal)
    {
        var normalized = string.IsNullOrWhiteSpace(target) ? "_self" : target.Trim();
        return normalized switch
        {
            "_self" or "_blank" or "_parent" or "_top" => normalized,
            _ => isExternal ? "_blank" : "_self"
        };
    }

    private sealed record LinkTargetOption(string Value, string Text);
}
