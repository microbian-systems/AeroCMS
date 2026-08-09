using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Selects one entry from a flat content type, optionally constrained by another reference.
/// </summary>
public partial class ContentReferencePicker : IAsyncDisposable
{
    [Parameter] public long? TargetContentTypeId { get; set; }
    [Parameter] public string? Culture { get; set; }
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter] public string? FilterField { get; set; }
    [Parameter] public string? FilterValue { get; set; }
    [Parameter] public string? DependencyLabel { get; set; }

    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    private readonly List<ReferencePickerOption> _options = [];
    private CancellationTokenSource? _loadCancellation;
    private long? _loadedTargetContentTypeId;
    private string? _loadedCulture;
    private string? _loadedFilterField;
    private string? _loadedFilterValue;
    private string _search = string.Empty;
    private bool _isLoading;
    private string? _error;

    private bool RequiresDependency => !string.IsNullOrWhiteSpace(FilterField);

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedTargetContentTypeId == TargetContentTypeId
            && string.Equals(_loadedCulture, Culture, StringComparison.Ordinal)
            && string.Equals(_loadedFilterField, FilterField, StringComparison.Ordinal)
            && string.Equals(_loadedFilterValue, FilterValue, StringComparison.Ordinal))
        {
            return;
        }

        _search = string.Empty;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();

        _loadedTargetContentTypeId = TargetContentTypeId;
        _loadedCulture = Culture;
        _loadedFilterField = FilterField;
        _loadedFilterValue = FilterValue;
        _options.Clear();
        _error = null;

        if (TargetContentTypeId is null or <= 0)
        {
            _error = "Choose a target content type in the field settings.";
            return;
        }

        if (RequiresDependency && string.IsNullOrWhiteSpace(FilterValue))
        {
            return;
        }

        _isLoading = true;
        try
        {
            var result = await ContentItemsApi.GetReferenceOptionsAsync(
                TargetContentTypeId.Value,
                Culture,
                _search,
                FilterField,
                FilterValue,
                ct: _loadCancellation.Token);
            switch (result)
            {
                case Result<IReadOnlyList<ContentReferenceOption>, AeroError>.Ok ok:
                    _options.AddRange(ok.Value.Select(
                        option => new ReferencePickerOption(
                            option.Id.ToString(
                                System.Globalization.CultureInfo.InvariantCulture),
                            option.Title,
                            option.Slug,
                            option.Culture)));
                    break;
                case Result<IReadOnlyList<ContentReferenceOption>, AeroError>.Failure failure:
                    _error = failure.Error.ToString();
                    break;
            }
        }
        catch (OperationCanceledException) when (_loadCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnSearchChangedAsync(string value)
    {
        _search = value ?? string.Empty;
        await ReloadAsync();
    }

    private Task OnValueChangedAsync(string? value) =>
        ValueChanged.InvokeAsync(value);

    public ValueTask DisposeAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record ReferencePickerOption(
        string Value,
        string Title,
        string Slug,
        string Culture)
    {
        public string DisplayText =>
            string.IsNullOrWhiteSpace(Slug)
                ? $"{Title} · {Culture}"
                : $"{Title} · /{Slug} · {Culture}";
    }
}
