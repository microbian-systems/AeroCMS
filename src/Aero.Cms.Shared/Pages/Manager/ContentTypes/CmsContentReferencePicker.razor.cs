using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Chooses a first-class CMS document by source and then by searchable item.
/// </summary>
public partial class CmsContentReferencePicker : IAsyncDisposable
{
    [Parameter]
    public string? Culture { get; set; }

    [Parameter]
    public CmsContentReferenceValue? Value { get; set; }

    [Parameter]
    public IReadOnlyList<string> AllowedSources { get; set; } = [];

    [Parameter]
    public EventCallback<CmsContentReferenceValue?> ValueChanged { get; set; }

    [Inject]
    private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    [Inject]
    private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private readonly List<CmsContentReferenceSource> _sources = [];
    private readonly List<CmsReferencePickerOption> _options = [];
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private bool _sourcesLoaded;
    private bool _loadingSources;
    private bool _loadingOptions;
    private string _selectedSource = string.Empty;
    private string? _loadedSource;
    private string? _loadedCulture;
    private string _searchText = string.Empty;
    private string? _error;

    private string SourceEntryLabel =>
        _sources.FirstOrDefault(source =>
            string.Equals(source.Key, _selectedSource, StringComparison.Ordinal))
        is { } selected
            ? L["{0} entry", selected.DisplayName.TrimEnd('s')]
            : L["Content entry"];

    protected override async Task OnParametersSetAsync()
    {
        if (!_sourcesLoaded)
        {
            await LoadSourcesAsync();
        }

        var desiredSource = Value?.Source ?? _selectedSource;
        if (!string.IsNullOrWhiteSpace(desiredSource)
            && !string.Equals(_selectedSource, desiredSource, StringComparison.Ordinal))
        {
            _selectedSource = desiredSource;
        }

        if (!string.IsNullOrWhiteSpace(_selectedSource)
            && (!string.Equals(_loadedSource, _selectedSource, StringComparison.Ordinal)
                || !string.Equals(_loadedCulture, Culture, StringComparison.Ordinal)))
        {
            await LoadOptionsAsync(_searchText);
        }
    }

    private async Task LoadSourcesAsync()
    {
        _loadingSources = true;
        _error = null;
        try
        {
            var result = await ContentItemsApi.GetCmsReferenceSourcesAsync();
            switch (result)
            {
                case Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>.Ok ok:
        IEnumerable<string> allowed = AllowedSources.Count == 0
            ? CmsContentReferenceSources.All
            : AllowedSources;
                    _sources.AddRange(ok.Value.Where(source =>
                        CmsContentReferenceSources.IsAllowedSource(
                            source.Key,
                            allowed)));
                    _sourcesLoaded = true;
                    break;
                case Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>.Failure failure:
                    _error = failure.Error.ToString();
                    break;
            }
        }
        finally
        {
            _loadingSources = false;
        }
    }

    private async Task OnSourceChangedAsync(ChangeEventArgs args)
    {
        _selectedSource = args.Value?.ToString() ?? string.Empty;
        _loadedSource = null;
        _searchText = string.Empty;
        _options.Clear();
        _error = null;
        await ValueChanged.InvokeAsync(null);
        if (!string.IsNullOrWhiteSpace(_selectedSource))
        {
            await LoadOptionsAsync();
        }
    }

    private async Task OnSearchTextChangedAsync(string value)
    {
        _searchText = value;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var ct = _searchCancellation.Token;
        try
        {
            await Task.Delay(250, ct);
            await LoadOptionsAsync(_searchText);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task LoadOptionsAsync(string? search = null)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        _loadingOptions = true;
        _error = null;
        _options.Clear();
        try
        {
            var result = await ContentItemsApi.GetCmsReferenceOptionsAsync(
                _selectedSource,
                Culture,
                search,
                take: 100,
                ct: _loadCancellation.Token);
            switch (result)
            {
                case Result<IReadOnlyList<CmsContentReferenceOption>, AeroError>.Ok ok:
                    _options.AddRange(ok.Value.Select(option =>
                        new CmsReferencePickerOption(
                            option.Id,
                            option.Title,
                            option.Slug,
                            option.Culture)));
                    _loadedSource = _selectedSource;
                    _loadedCulture = Culture;
                    break;
                case Result<IReadOnlyList<CmsContentReferenceOption>, AeroError>.Failure failure:
                    _error = failure.Error.ToString();
                    break;
            }
        }
        catch (OperationCanceledException) when (_loadCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _loadingOptions = false;
        }
    }

    private Task OnOptionChangedAsync(string? id) =>
        ValueChanged.InvokeAsync(
            string.IsNullOrWhiteSpace(id)
                ? null
                : new CmsContentReferenceValue(_selectedSource, id));

    public ValueTask DisposeAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record CmsReferencePickerOption(
        string Value,
        string Title,
        string Slug,
        string Culture)
    {
        public string DisplayText =>
            string.IsNullOrWhiteSpace(Slug)
                ? $"{Title} · {Culture}"
                : $"{Title} · /{Slug.TrimStart('/')} · {Culture}";
    }
}
