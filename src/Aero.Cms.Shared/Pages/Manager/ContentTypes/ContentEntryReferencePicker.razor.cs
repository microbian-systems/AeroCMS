using System.Text;
using System.Text.Json;
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
    [Parameter] public IReadOnlyList<string> PreviewFields { get; set; } = [];
    [Parameter] public EventCallback<ContentEntryKey?> ValueChanged { get; set; }
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private IContentViewsHttpClient ContentViewsApi { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private readonly List<CmsContentReferenceSource> _providers = [];
    private readonly List<EntryOption> _options = [];
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _previewCancellation;
    private bool _providersLoaded;
    private bool _loadingProviders;
    private bool _loadingOptions;
    private string _selectedProvider = string.Empty;
    private string? _loadedProvider;
    private string? _loadedCulture;
    private string _searchText = string.Empty;
    private string? _error;
    private readonly string _selectionHelpId = $"content-entry-selection-help-{Guid.NewGuid():N}";
    private readonly string _previewHeadingId = $"content-entry-preview-heading-{Guid.NewGuid():N}";
    private ContentEntryKey? _effectivePreviewKey;
    private ContentEntryKey? _previewRequestKey;
    private VirtualContentEntryDetail? _previewEntry;
    private bool _loadingPreview;
    private string? _previewError;
    private readonly ContentEntryPreviewRequestGuard _previewRequestGuard = new();

    private IReadOnlyList<string> PreviewFieldNames =>
        ContentEntryReferencePreviewUi.NormalizePreviewFields(PreviewFields);

    private IEnumerable<string> VisiblePreviewFieldNames =>
        PreviewFieldNames.Take(ContentEntryReferencePreviewUi.MaximumPreviewFields);

    protected override async Task OnParametersSetAsync()
    {
        if (!_providersLoaded) await LoadProvidersAsync();
        var desired = Value?.Provider ?? _selectedProvider;
        if (!string.IsNullOrWhiteSpace(desired) && !string.Equals(desired, _selectedProvider, StringComparison.Ordinal)) _selectedProvider = desired;
        if (!string.IsNullOrWhiteSpace(_selectedProvider) && (!string.Equals(_loadedProvider, _selectedProvider, StringComparison.Ordinal) || !string.Equals(_loadedCulture, Culture, StringComparison.Ordinal))) await LoadOptionsAsync(_searchText);
        _effectivePreviewKey = Value is { IsValid: true } value ? value : null;
        await EnsurePreviewAsync(_effectivePreviewKey);
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
        _effectivePreviewKey = null;
        ClearPreview();
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

    private async Task OnOptionChangedAsync(string? stableId)
    {
        ContentEntryKey? key = string.IsNullOrWhiteSpace(stableId)
            ? null
            : new ContentEntryKey(_selectedProvider, stableId);
        _effectivePreviewKey = key;
        await ValueChanged.InvokeAsync(key);
        await EnsurePreviewAsync(key);
    }

    private Task RetryPreviewAsync() => LoadPreviewAsync(_effectivePreviewKey);

    private Task EnsurePreviewAsync(ContentEntryKey? key)
    {
        if (key is null || PreviewFieldNames.Count == 0)
        {
            ClearPreview();
            return Task.CompletedTask;
        }

        if (_previewRequestKey == key
            && (_loadingPreview || _previewEntry is not null || _previewError is not null))
        {
            return Task.CompletedTask;
        }

        return LoadPreviewAsync(key);
    }

    private async Task LoadPreviewAsync(ContentEntryKey? key)
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        var requestVersion = _previewRequestGuard.Begin();
        _previewRequestKey = key;
        _previewEntry = null;
        _previewError = null;

        if (key is not { IsValid: true } selected || PreviewFieldNames.Count == 0)
        {
            _loadingPreview = false;
            return;
        }

        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        _loadingPreview = true;
        try
        {
            var result = await ContentViewsApi.GetEntryAsync(
                selected.Provider,
                selected.StableId,
                cancellation.Token);
            if (!_previewRequestGuard.IsCurrent(requestVersion) || cancellation.IsCancellationRequested)
            {
                return;
            }

            if (result is Result<VirtualContentEntryDetail, AeroError>.Ok ok
                && string.Equals(ok.Value.Provider, selected.Provider, StringComparison.Ordinal)
                && string.Equals(ok.Value.StableId, selected.StableId, StringComparison.Ordinal))
            {
                _previewEntry = ok.Value;
            }
            else if (result is Result<VirtualContentEntryDetail, AeroError>.Failure)
            {
                _previewError = L["The selected entry preview could not be loaded."];
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (_previewRequestGuard.IsCurrent(requestVersion))
            {
                _previewError = L["The selected entry preview could not be loaded."];
            }
        }
        finally
        {
            if (_previewRequestGuard.IsCurrent(requestVersion))
            {
                _loadingPreview = false;
            }
        }
    }

    private void ClearPreview()
    {
        _previewRequestGuard.Invalidate();
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = null;
        _previewRequestKey = null;
        _previewEntry = null;
        _previewError = null;
        _loadingPreview = false;
    }

    public ValueTask DisposeAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        ClearPreview();
        return ValueTask.CompletedTask;
    }

    private sealed record EntryOption(string Value, string Title, string? Detail) { public string DisplayText => string.IsNullOrWhiteSpace(Detail) ? Title : $"{Title} · {Detail}"; }
}

/// <summary>Pure, bounded formatting used by the content-entry reference preview.</summary>
public static class ContentEntryReferencePreviewUi
{
    public const int MaximumPreviewFields = 20;
    public const int MaximumFieldNameCharacters = 128;
    public const int MaximumCollectionItems = 12;
    public const int MaximumDepth = 3;
    public const int MaximumStringCharacters = 320;
    public const int MaximumRenderedCharacters = 2_000;

    public static IReadOnlyList<string> ParsePreviewFields(string? value) =>
        NormalizePreviewFields((value ?? string.Empty).Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static IReadOnlyList<string> ReadPreviewFields(
        IReadOnlyDictionary<string, JsonElement> settings,
        string settingName)
    {
        if (!settings.TryGetValue(settingName, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return NormalizePreviewFields(value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty));
    }

    public static IReadOnlyList<string> NormalizePreviewFields(IEnumerable<string>? fields)
    {
        if (fields is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var field in fields)
        {
            var candidate = field.Trim();
            if (candidate.Length is > 0 and <= MaximumFieldNameCharacters
                && seen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized;
    }

    public static ContentEntryPreviewValue FormatValue(JsonElement value)
    {
        var writer = new PreviewValueWriter();
        writer.Write(value, 0, topLevel: true);
        return new ContentEntryPreviewValue(
            writer.Text,
            ValueKind(value),
            writer.IsTruncated);
    }

    private static string ValueKind(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "text",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "list",
        _ => "unknown"
    };

    private sealed class PreviewValueWriter
    {
        private readonly StringBuilder _builder = new();

        public string Text => _builder.ToString();
        public bool IsTruncated { get; private set; }

        public void Write(JsonElement value, int depth, bool topLevel = false)
        {
            if (_builder.Length >= MaximumRenderedCharacters)
            {
                IsTruncated = true;
                return;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    WriteString(value.GetString() ?? string.Empty, quoted: !topLevel);
                    break;
                case JsonValueKind.Number:
                    Append(value.GetRawText());
                    break;
                case JsonValueKind.True:
                    Append("True");
                    break;
                case JsonValueKind.False:
                    Append("False");
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    Append("Null");
                    break;
                case JsonValueKind.Object:
                    WriteObject(value, depth);
                    break;
                case JsonValueKind.Array:
                    WriteArray(value, depth);
                    break;
                default:
                    Append("Unavailable");
                    break;
            }
        }

        private void WriteObject(JsonElement value, int depth)
        {
            if (depth >= MaximumDepth)
            {
                Append("{ … }");
                IsTruncated = true;
                return;
            }

            Append("{ ");
            var index = 0;
            foreach (var property in value.EnumerateObject())
            {
                if (index == MaximumCollectionItems)
                {
                    Append(index == 0 ? "…" : ", …");
                    IsTruncated = true;
                    break;
                }

                if (index++ > 0) Append(", ");
                WriteString(property.Name, quoted: true);
                Append(": ");
                Write(property.Value, depth + 1);
            }

            Append(" }");
        }

        private void WriteArray(JsonElement value, int depth)
        {
            if (depth >= MaximumDepth)
            {
                Append("[ … ]");
                IsTruncated = true;
                return;
            }

            Append("[ ");
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (index == MaximumCollectionItems)
                {
                    Append(index == 0 ? "…" : ", …");
                    IsTruncated = true;
                    break;
                }

                if (index++ > 0) Append(", ");
                Write(item, depth + 1);
            }

            Append(" ]");
        }

        private void WriteString(string value, bool quoted)
        {
            if (quoted) Append("\"");
            var length = Math.Min(value.Length, MaximumStringCharacters);
            for (var index = 0; index < length; index++)
            {
                var character = value[index];
                switch (character)
                {
                    case '\\': Append("\\\\"); break;
                    case '"': Append("\\\""); break;
                    case '\r': Append("\\r"); break;
                    case '\n': Append("\\n"); break;
                    case '\t': Append("\\t"); break;
                    default:
                        if (char.IsControl(character))
                        {
                            Append($"\\u{(int)character:X4}");
                        }
                        else
                        {
                            Append(character.ToString());
                        }
                        break;
                }
            }

            if (value.Length > MaximumStringCharacters)
            {
                Append("…");
                IsTruncated = true;
            }
            if (quoted) Append("\"");
        }

        private void Append(string value)
        {
            var remaining = MaximumRenderedCharacters - _builder.Length;
            if (remaining <= 0)
            {
                IsTruncated = true;
                return;
            }

            if (value.Length <= remaining)
            {
                _builder.Append(value);
                return;
            }

            if (remaining == 1)
            {
                _builder.Append('…');
            }
            else
            {
                _builder.Append(value.AsSpan(0, remaining - 1));
                _builder.Append('…');
            }
            IsTruncated = true;
        }
    }
}

public sealed record ContentEntryPreviewValue(string Text, string Kind, bool IsTruncated);

/// <summary>Monotonic guard that prevents an older async preview response from replacing newer state.</summary>
public sealed class ContentEntryPreviewRequestGuard
{
    private int _version;

    public int Begin() => ++_version;

    public void Invalidate() => ++_version;

    public bool IsCurrent(int version) => version == _version;
}
