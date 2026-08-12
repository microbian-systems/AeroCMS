using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>Chooses a provider-qualified entry from a server-scoped virtual content source.</summary>
public partial class ContentEntryReferencePicker : IAsyncDisposable
{
    [Parameter] public string? Culture { get; set; }
    [Parameter] public ContentEntryKey? Value { get; set; }
    [Parameter] public IReadOnlyList<string> AllowedProviders { get; set; } = [];
    [Parameter] public EventCallback<ContentEntryKey?> ValueChanged { get; set; }
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private readonly List<CmsContentReferenceSource> _providers = [];
    private readonly List<EntryOption> _options = [];
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private bool _providersLoaded;
    private bool _loadingProviders;
    private bool _loadingOptions;
    private string _selectedProvider = string.Empty;
    private string? _loadedProvider;
    private string? _loadedCulture;
    private string _searchText = string.Empty;
    private string? _error;

    protected override async Task OnParametersSetAsync()
    {
        if (!_providersLoaded) await LoadProvidersAsync();
        var desired = Value?.Provider ?? _selectedProvider;
        if (!string.IsNullOrWhiteSpace(desired) && !string.Equals(desired, _selectedProvider, StringComparison.Ordinal)) _selectedProvider = desired;
        if (!string.IsNullOrWhiteSpace(_selectedProvider) && (!string.Equals(_loadedProvider, _selectedProvider, StringComparison.Ordinal) || !string.Equals(_loadedCulture, Culture, StringComparison.Ordinal))) await LoadOptionsAsync(_searchText);
    }

    private async Task LoadProvidersAsync()
    {
        _loadingProviders = true; _error = null;
        try
        {
            var result = await ContentItemsApi.GetContentEntryReferenceSourcesAsync();
            if (result is Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>.Ok ok)
            {
                var allowed = AllowedProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);
                _providers.AddRange(ok.Value.Where(source => allowed.Count == 0 || allowed.Contains(source.Key)));
                _providersLoaded = true;
            }
            else if (result is Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>.Failure failure) _error = failure.Error.ToString();
        }
        finally { _loadingProviders = false; }
    }

    private async Task OnProviderChangedAsync(ChangeEventArgs args)
    {
        _selectedProvider = args.Value?.ToString() ?? string.Empty; _loadedProvider = null; _searchText = string.Empty; _options.Clear(); _error = null;
        await ValueChanged.InvokeAsync(null);
        if (!string.IsNullOrWhiteSpace(_selectedProvider)) await LoadOptionsAsync();
    }

    private async Task OnSearchTextChangedAsync(string value)
    {
        _searchText = value; _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); _searchCancellation = new CancellationTokenSource();
        try { await Task.Delay(250, _searchCancellation.Token); await LoadOptionsAsync(value); }
        catch (OperationCanceledException) when (_searchCancellation.IsCancellationRequested) { }
    }

    private async Task LoadOptionsAsync(string? search = null)
    {
        _loadCancellation?.Cancel(); _loadCancellation?.Dispose(); _loadCancellation = new CancellationTokenSource();
        _loadingOptions = true; _error = null; _options.Clear();
        try
        {
            var result = await ContentItemsApi.GetContentEntryReferenceOptionsAsync(_selectedProvider, Culture, search, 100, _loadCancellation.Token);
            if (result is Result<IReadOnlyList<ContentEntryReferenceOption>, AeroError>.Ok ok)
            {
                _options.AddRange(ok.Value.Where(option => string.Equals(option.Provider, _selectedProvider, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(option.StableId)).Select(option => new EntryOption(option.StableId, option.Title, option.Detail)));
                _loadedProvider = _selectedProvider; _loadedCulture = Culture;
            }
            else if (result is Result<IReadOnlyList<ContentEntryReferenceOption>, AeroError>.Failure failure) _error = failure.Error.ToString();
        }
        catch (OperationCanceledException) when (_loadCancellation.IsCancellationRequested) { }
        finally { _loadingOptions = false; }
    }

    private Task OnOptionChangedAsync(string? stableId) => ValueChanged.InvokeAsync(string.IsNullOrWhiteSpace(stableId) ? null : new ContentEntryKey(_selectedProvider, stableId));
    public ValueTask DisposeAsync() { _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); _loadCancellation?.Cancel(); _loadCancellation?.Dispose(); return ValueTask.CompletedTask; }
    private sealed record EntryOption(string Value, string Title, string? Detail) { public string DisplayText => string.IsNullOrWhiteSpace(Detail) ? Title : $"{Title} · {Detail}"; }
}
