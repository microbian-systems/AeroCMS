using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

public sealed partial class VirtualContentListSelector
{
    [Parameter] public IReadOnlyList<ContentEntryProviderOption> Providers { get; set; } = [];
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback<VirtualContentListRequest> AddRequested { get; set; }

    private int PageSize { get; set; } = 10;
    private string SelectedProvider { get; set; } = string.Empty;
    private string SearchText { get; set; } = string.Empty;
    private string? LocalError { get; set; }
    private string? DisplayError => LocalError ?? ErrorMessage;
    private bool CanAdd => !IsLoading
        && LocalError is null
        && Providers.Any(option => string.Equals(option.Provider, SelectedProvider, StringComparison.Ordinal))
        && PageSize is >= 1 and <= 100
        && SearchText.Length <= 256;

    private void OnProviderChangedAsync(ChangeEventArgs args)
    {
        SelectedProvider = args.Value?.ToString()?.Trim() ?? string.Empty;
        LocalError = null;
    }

    private void OnPageSizeChanged(ChangeEventArgs args)
    {
        LocalError = null;
        if (!int.TryParse(args.Value?.ToString(), out var value) || value is < 1 or > 100)
        {
            LocalError = "Page size must be between 1 and 100.";
            return;
        }

        PageSize = value;
    }

    private void OnSearchChanged(ChangeEventArgs args)
    {
        SearchText = args.Value?.ToString() ?? string.Empty;
        LocalError = SearchText.Length > 256
            ? "Search text cannot exceed 256 characters."
            : null;
    }

    private Task AddAsync()
    {
        if (!CanAdd)
        {
            LocalError ??= "Select an available provider and enter valid bounded list settings.";
            return Task.CompletedTask;
        }

        return AddRequested.InvokeAsync(new VirtualContentListRequest(
            SelectedProvider,
            PageSize,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()));
    }
}

public sealed record VirtualContentListRequest(string Provider, int PageSize, string? SearchText);
