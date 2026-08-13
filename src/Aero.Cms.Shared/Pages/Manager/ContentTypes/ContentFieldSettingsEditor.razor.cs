using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Reusable settings surface for a content-type field.
/// </summary>
public partial class ContentFieldSettingsEditor
{
    private const string ContentEntryPreviewFieldsSetting = "previewFields";

    private static IReadOnlyList<AeroAiFieldExposure> AiExposureOptions { get; } =
        Enum.GetValues<AeroAiFieldExposure>();

    private ContentTypeDetail? _referenceTargetDefinition;
    private long? _loadedReferenceTargetId;
    private IReadOnlyList<CmsContentReferenceSource> _contentEntryProviders = [];
    private bool _contentEntryProvidersLoaded;

    [Parameter, EditorRequired]
    public ContentFieldDefinition Field { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<ContentFieldDefinition> OwnerFields { get; set; } = [];

    [Parameter]
    public IReadOnlyList<ContentTypeSummary> ContentTypes { get; set; } = [];

    [Parameter]
    public string CurrentContentTypeAlias { get; set; } = string.Empty;

    [Parameter]
    public string FieldTypeLabel { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<ContentFieldDefinition> FieldChanged { get; set; }

    [Parameter]
    public bool LocalizationModeLocked { get; set; }

    [Parameter]
    public string? LocalizationModeLockedReason { get; set; }

    [Inject]
    private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;

    [Inject]
    private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;

    [Inject]
    private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private bool IsCmsDocumentReference =>
        string.Equals(
            GetStringSetting(ReferenceContentFieldSettings.TargetKind),
            ReferenceContentFieldSettings.TargetKindCmsDocument,
            StringComparison.Ordinal);

    private bool IsContentEntryReference =>
        string.Equals(
            GetStringSetting(ReferenceContentFieldSettings.TargetKind),
            ReferenceContentFieldSettings.TargetKindContentEntry,
            StringComparison.Ordinal);

    private IReadOnlySet<string> AllowedContentEntryProviders =>
        Field.Settings.TryGetValue(ReferenceContentFieldSettings.AllowedProviders, out var providers)
        && providers.ValueKind == JsonValueKind.Array
            ? providers.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private bool IsHierarchyReference =>
        string.Equals(
            GetStringSetting(ReferenceContentFieldSettings.SelectionMode),
            ReferenceContentFieldSettings.SelectionModeHierarchy,
            StringComparison.Ordinal);

    private bool IsCompositeField =>
        Field.FieldType is ContentFieldTypes.List or ContentFieldTypes.Gallery or ContentFieldTypes.Dictionary;

    private bool SupportsFullTextSearch =>
        Field.FieldType is ContentFieldTypes.Text or ContentFieldTypes.RichText or
            ContentFieldTypes.Url or ContentFieldTypes.List or ContentFieldTypes.Dictionary;

    private bool SupportsSemanticSearch =>
        Field.FieldType is ContentFieldTypes.Text or ContentFieldTypes.RichText;

    private int RangeMinimum =>
        GetBoolSetting(RangeContentFieldSettings.AllowNegative)
            ? int.MinValue
            : 0;

    private string LocalizationModeHelp => Field.LocalizationMode switch
    {
        ContentFieldLocalizationMode.Shared =>
            L["Use for invariant values such as identifiers or source references. One translation-group value is shown in every culture."],
        ContentFieldLocalizationMode.Localized =>
            L["Use when every culture must supply its own value. A new translation starts empty."],
        _ =>
            L["Use when a new translation should start from the source value but become independent after the fork."]
    };

    private IEnumerable<ContentTypeSummary> AvailableReferenceContentTypes =>
        ContentTypes.Where(contentType =>
            !IsHierarchyReference || contentType.Structure == ContentStructure.Hierarchical);

    private IEnumerable<ContentFieldDefinition> DependencyReferenceFields
    {
        get
        {
            var selectedIndex = FindOwnerFieldIndex();
            return OwnerFields
                .Take(Math.Max(0, selectedIndex))
                .Where(candidate =>
                    candidate.FieldType == ContentFieldTypes.Reference &&
                    !IsCmsReference(candidate));
        }
    }

    private IEnumerable<ContentFieldDefinition> TargetReferenceFields
    {
        get
        {
            var targetId = GetTargetContentTypeId();
            if (targetId is not null && ContentTypes.Any(type => type.Id == targetId && string.Equals(type.Alias, CurrentContentTypeAlias, StringComparison.Ordinal)))
            {
                return OwnerFields.Where(candidate =>
                    candidate.FieldType == ContentFieldTypes.Reference &&
                    !IsCmsReference(candidate));
            }

            return _referenceTargetDefinition?.Fields.Where(candidate =>
                       candidate.FieldType == ContentFieldTypes.Reference &&
                       !IsCmsReference(candidate))
                   ?? [];
        }
    }

    private string AllowedValuesText
    {
        get
        {
            if (!TryGetSetting(CompositeContentFieldSettings.AllowedValues, out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item)));
        }
    }

    private string PreviewFieldsText => string.Join(
        Environment.NewLine,
        ContentEntryReferencePreviewUi.ReadPreviewFields(
            Field.Settings,
            ContentEntryPreviewFieldsSetting));

    protected override async Task OnParametersSetAsync()
    {
        await LoadReferenceTargetDefinitionAsync();
        if (IsContentEntryReference && !_contentEntryProvidersLoaded)
            await LoadContentEntryProvidersAsync();
    }

    private async Task LoadContentEntryProvidersAsync()
    {
        var result = await ContentItemsApi.GetContentEntryReferenceSourcesAsync();
        if (result is Result<IReadOnlyList<CmsContentReferenceSource>, AeroError>.Ok ok)
        {
            _contentEntryProviders = ok.Value;
            _contentEntryProvidersLoaded = true;
        }
    }

    private async Task SetContentEntryProviderAsync(string provider, bool selected)
    {
        var values = AllowedContentEntryProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected) values.Add(provider); else values.Remove(provider);
        Field.Settings[ReferenceContentFieldSettings.AllowedProviders] =
            JsonSerializer.SerializeToElement(values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(), ContentJsonContext.Default.ListString);
        await NotifyChangedAsync();
    }

    private async Task SetPreviewFieldsTextAsync(string? value)
    {
        var fields = ContentEntryReferencePreviewUi.ParsePreviewFields(value);
        if (fields.Count == 0)
        {
            Field.Settings.Remove(ContentEntryPreviewFieldsSetting);
        }
        else
        {
            Field.Settings[ContentEntryPreviewFieldsSetting] =
                JsonSerializer.SerializeToElement(fields.ToList(), ContentJsonContext.Default.ListString);
        }

        await NotifyChangedAsync();
    }

    private async Task SetLabelAsync(string? value)
    {
        Field.Label = value;
        await NotifyChangedAsync();
    }

    private async Task SetPlaceholderAsync(string? value)
    {
        Field.Placeholder = value;
        await NotifyChangedAsync();
    }

    private async Task SetRequiredAsync(bool value)
    {
        Field.Required = value;
        await NotifyChangedAsync();
    }

    private async Task SetLocalizationModeAsync(ChangeEventArgs args)
    {
        if (LocalizationModeLocked)
        {
            return;
        }

        if (Enum.TryParse<ContentFieldLocalizationMode>(
                args.Value?.ToString(),
                ignoreCase: true,
                out var mode))
        {
            Field.LocalizationMode = mode;
            await NotifyChangedAsync();
        }
    }

    private async Task SetNameAsync(string? value)
    {
        Field.Name = value?.Trim() ?? string.Empty;
        await NotifyChangedAsync();
    }

    private async Task SetDefaultValueAsync(string? value)
    {
        Field.DefaultValue = value;
        await NotifyChangedAsync();
    }

    private async Task SetIndexedAsync(bool value)
    {
        Field.Indexed = Field.FieldType == ContentFieldTypes.Reference || value;
        await NotifyChangedAsync();
    }

    private async Task SetFullTextAsync(bool value)
    {
        Field.FullTextSearchable = value;
        await NotifyChangedAsync();
    }

    private async Task SetSemanticAsync(bool value)
    {
        Field.SemanticSearchable = value;
        await NotifyChangedAsync();
    }

    private async Task SetAiExposureAsync(AeroAiFieldExposure value)
    {
        Field.AiExposure = value;
        await NotifyChangedAsync();
    }

    private async Task SetAllowedValuesTextAsync(string? value)
    {
        var values = (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        Field.Settings[CompositeContentFieldSettings.AllowedValues] =
            JsonSerializer.SerializeToElement(values, ContentJsonContext.Default.ListString);
        await NotifyChangedAsync();
    }

    private async Task SetRangeAllowNegativeAsync(bool allowNegative)
    {
        SetSetting(RangeContentFieldSettings.AllowNegative, allowNegative);
        if (!allowNegative)
        {
            if ((GetIntSetting(RangeContentFieldSettings.Start) ?? 0) < 0)
            {
                SetSetting(RangeContentFieldSettings.Start, 0);
            }

            if ((GetIntSetting(RangeContentFieldSettings.End) ?? 0) < 0)
            {
                SetSetting(RangeContentFieldSettings.End, 0);
            }
        }

        await NotifyChangedAsync();
    }

    private async Task OnReferenceTargetChangedAsync(ChangeEventArgs args)
    {
        SetSetting(
            ReferenceContentFieldSettings.TargetContentTypeId,
            args.Value?.ToString());
        Field.Indexed = true;
        Field.Settings.Remove(ReferenceContentFieldSettings.TargetFilterField);
        _loadedReferenceTargetId = null;
        _referenceTargetDefinition = null;
        await LoadReferenceTargetDefinitionAsync();
        await NotifyChangedAsync();
    }

    private async Task OnReferenceDependencyChangedAsync(ChangeEventArgs args)
    {
        SetSetting(
            ReferenceContentFieldSettings.DependsOnField,
            args.Value?.ToString());
        Field.Settings.Remove(ReferenceContentFieldSettings.TargetFilterField);
        await NotifyChangedAsync();
    }

    private async Task SetSettingAsync<T>(string key, T value)
    {
        SetSetting(key, value);
        await NotifyChangedAsync();
    }

    private void SetSetting<T>(string key, T value)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            Field.Settings.Remove(key);
            return;
        }

        Field.Settings[key] = JsonSerializer.SerializeToElement(value);
    }

    private int? GetIntSetting(string key) =>
        TryGetSetting(key, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private decimal? GetDecimalSetting(string key) =>
        TryGetSetting(key, out var value) && value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private string? GetStringSetting(string key)
    {
        if (!TryGetSetting(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private bool GetBoolSetting(string key, bool fallback = false) =>
        TryGetSetting(key, out var value)
            ? value.ValueKind == JsonValueKind.True
            : fallback;

    private bool TryGetSetting(string key, out JsonElement value) =>
        Field.Settings.TryGetValue(key, out value);

    private async Task LoadReferenceTargetDefinitionAsync()
    {
        if (Field.FieldType != ContentFieldTypes.Reference || IsCmsDocumentReference)
        {
            return;
        }

        var targetId = GetTargetContentTypeId();
        if (targetId is null || targetId <= 0 || targetId == _loadedReferenceTargetId)
        {
            return;
        }

        var result = await ContentTypesApi.GetByIdAsync(targetId.Value);
        if (result is Result<ContentTypeDetail, AeroError>.Ok ok)
        {
            _referenceTargetDefinition = ok.Value;
            _loadedReferenceTargetId = targetId;
        }
    }

    private long? GetTargetContentTypeId()
    {
        var value = GetStringSetting(ReferenceContentFieldSettings.TargetContentTypeId);
        return long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }

    private Task NotifyChangedAsync() =>
        FieldChanged.HasDelegate
            ? FieldChanged.InvokeAsync(Field)
            : Task.CompletedTask;

    private static string FieldLabel(ContentFieldDefinition field) =>
        string.IsNullOrWhiteSpace(field.Label)
            ? field.Name
            : field.Label!;

    private static bool IsCmsReference(ContentFieldDefinition field) =>
        field.Settings.TryGetValue(
            ReferenceContentFieldSettings.TargetKind,
            out var targetKind)
        && targetKind.ValueKind == JsonValueKind.String
        && string.Equals(
            targetKind.GetString(),
            ReferenceContentFieldSettings.TargetKindCmsDocument,
            StringComparison.Ordinal);

    private int FindOwnerFieldIndex()
    {
        for (var index = 0; index < OwnerFields.Count; index++)
        {
            if (ReferenceEquals(OwnerFields[index], Field)
                || string.Equals(
                    OwnerFields[index].Name,
                    Field.Name,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return OwnerFields.Count;
    }
}
