using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Represents a class for ContentTypeEditor.
/// </summary>
public partial class ContentTypeEditor
{
    private const int MaximumHierarchyDepthLimit = 32;
    private const string HierarchyReferenceFieldOption = "hierarchy-reference";
    private const string CmsReferenceFieldOption = "cms-reference";
    private static IReadOnlyList<AeroAiFieldExposure> AiExposureOptions { get; } =
        Enum.GetValues<AeroAiFieldExposure>();

        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
[Parameter] public string? Alias { get; set; }

    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private List<FieldTypeOption> FieldOptions =>
    [
        new("text", L["Short text"], "title", L["Single line names, headlines, and labels."]),
        new("richtext", L["Rich text"], "notes", L["Longer formatted copy."]),
        new("image", L["Image"], "image", L["Photo or graphic URL."]),
        new(ContentFieldTypes.Gallery, L["Gallery"], "photo_library", L["Select and order multiple images."]),
        new("number", L["Number"], "pin", L["Prices, counts, rankings, or measurements."]),
        new(ContentFieldTypes.Range, L["Range"], "linear_scale", L["Choose one whole number from an inclusive range."]),
        new(ContentFieldTypes.List, L["List"], "format_list_bulleted", L["Choose from preset text or number values."]),
        new(ContentFieldTypes.Color, L["Color"], "palette", L["Choose a color, with optional transparency."]),
        new("boolean", L["Yes/No"], "toggle_on", L["A simple on/off choice."]),
        new("url", L["Link"], "link", L["Website or call-to-action URL."]),
        new("date", L["Date"], "event", L["Dates and milestones."]),
        new("reference", L["Related content"], "account_tree", L["Relate this item to an entry from another content type."]),
        new(HierarchyReferenceFieldOption, L["Hierarchy entry"], "family_history", L["Choose an entry from a hierarchy with its full path."]),
        new(CmsReferenceFieldOption, L["Site content"], "article", L["Choose an existing page, post, doc, or public content entry."]),
        new(ContentFieldTypes.Dictionary, L["Key/value"], "data_object", L["Add a small set of labeled values."])
    ];

    private bool IsNew => string.IsNullOrWhiteSpace(Alias);
    private EditorTab _activeTab = EditorTab.Basics;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _aliasLocked;
    private bool _useCustomTemplate;
    private int? _selectedFieldIndex;
    private StandaloneCodeEditor? _scribanEditor;
    private RadzenDataGrid<ContentItemSummary>? _entriesGrid;
    private IEnumerable<ContentItemSummary> _entries = [];
    private int _entriesCount;
    private bool _entriesLoading;
    private string _entriesSearchText = string.Empty;
    private IReadOnlyList<ContentTypeSummary> _availableParentContentTypes = [];
    private readonly Dictionary<long, ContentTypeDetail> _referenceTargetDefinitions = [];

    private string Name { get; set; } = string.Empty;
    private string AliasValue { get; set; } = string.Empty;
    private string? Description { get; set; }
    private string? Category { get; set; }
    private bool AllowPublicUrl { get; set; }
    private bool IncludeInSearch { get; set; } = true;
    private bool IncludeInPublicAi { get; set; }
    private ContentCardinality _cardinality = ContentCardinality.Collection;
    private ContentStructure _structure = ContentStructure.Flat;
    private ContentCardinality Cardinality
    {
        get => _cardinality;
        set => _cardinality = _structure == ContentStructure.Hierarchical
                              && value == ContentCardinality.Singleton
            ? ContentCardinality.Collection
            : value;
    }

    private ContentStructure Structure
    {
        get => _structure;
        set
        {
            _structure = value;
            if (value == ContentStructure.Hierarchical)
            {
                _cardinality = ContentCardinality.Collection;
            }
        }
    }

    private bool AllowRootItems { get; set; } = true;
    private int MaximumHierarchyDepth { get; set; } = 8;
    private bool RequireSameTypeParent { get; set; } = true;
    private IReadOnlyList<long> AllowedParentContentTypeIds { get; set; } = [];
    private string HierarchyOrdering { get; set; } = "sortOrder,title";
    private string? ScribanTemplate { get; set; }
    private List<ContentFieldDefinition> Fields { get; set; } = [];

    private string DisplayTypeName => string.IsNullOrWhiteSpace(Name) ? "content" : Name.ToLowerInvariant();

    private string EntriesDescription => AllowPublicUrl
        ? L["Managing {0} {1} entries with optional public pages.", _entriesCount, DisplayTypeName]
        : L["Managing {0} {1} entries for embedding in pages and blocks.", _entriesCount, DisplayTypeName];

    private ContentFieldDefinition? SelectedField =>
        _selectedFieldIndex is int index && index >= 0 && index < Fields.Count
            ? Fields[index]
            : null;

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        await LoadParentContentTypeOptionsAsync();

        if (IsNew)
        {
            _isLoading = false;
            return;
        }

        var result = await ContentTypesApi.GetByAliasAsync(Alias!);
        if (result is Result<ContentTypeDetail, AeroError>.Ok ok)
        {
            var detail = ok.Value;
            Name = detail.Name;
            AliasValue = detail.Alias;
            Description = detail.Description;
            Category = detail.Category;
            AllowPublicUrl = detail.AllowPublicUrl;
            IncludeInSearch = detail.IncludeInSearch;
            IncludeInPublicAi = detail.IncludeInPublicAi;
            Cardinality = detail.Cardinality;
            Structure = detail.Structure;
            var hierarchyRules = detail.HierarchyRules ?? new ContentHierarchyRules();
            AllowRootItems = hierarchyRules.AllowRootItems;
            MaximumHierarchyDepth = hierarchyRules.MaximumDepth;
            RequireSameTypeParent = hierarchyRules.RequireSameTypeParent;
            AllowedParentContentTypeIds = hierarchyRules.AllowedParentContentTypeIds;
            HierarchyOrdering = hierarchyRules.DefaultOrdering;
            ScribanTemplate = detail.ScribanTemplate;
            _useCustomTemplate = !string.IsNullOrWhiteSpace(detail.ScribanTemplate);
            Fields = detail.Fields.Select(CloneField).ToList();
            _aliasLocked = true;
            await LoadReferenceTargetDefinitionsAsync();
        }
        else if (result is Result<ContentTypeDetail, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Load failed", failure.Error.ToString());
            Navigation.NavigateTo("/manager/content-types");
        }

        _isLoading = false;
    }

    private async Task LoadParentContentTypeOptionsAsync()
    {
        var result = await ContentTypesApi.GetAllAsync();
        _availableParentContentTypes = result switch
        {
            Result<IReadOnlyList<ContentTypeSummary>, AeroError>.Ok ok => ok.Value,
            _ => []
        };
    }

    private void SetAllowedParentContentTypes(IReadOnlyList<long> selectedIds)
        => AllowedParentContentTypeIds = selectedIds;

    private void OnNameChanged(string value)
    {
        Name = value;
        if (!_aliasLocked)
        {
            AliasValue = GenerateHandle(value);
        }
    }

    private void OnAliasChanged(string value)
    {
        AliasValue = GenerateHandle(value);
        _aliasLocked = true;
    }

    private void AddField(string fieldType)
    {
        var option = GetFieldOption(fieldType);
        var baseLabel = option.Label == "Short text" ? "Title" : option.Label;
        var handle = CreateUniqueFieldName(GenerateHandle(baseLabel));
        var storedFieldType = fieldType is HierarchyReferenceFieldOption or CmsReferenceFieldOption
            ? ContentFieldTypes.Reference
            : fieldType;

        var field = new ContentFieldDefinition
        {
            Name = handle,
            FieldType = storedFieldType,
            Label = baseLabel,
            Placeholder = option.Description,
            Indexed = storedFieldType == ContentFieldTypes.Reference,
            FullTextSearchable =
                storedFieldType is ContentFieldTypes.Text or ContentFieldTypes.RichText
        };

        if (fieldType == HierarchyReferenceFieldOption)
        {
            SetSetting(
                field,
                ReferenceContentFieldSettings.SelectionMode,
                ReferenceContentFieldSettings.SelectionModeHierarchy);
            SetSetting(field, ReferenceContentFieldSettings.SelectLeafOnly, true);
            SetSetting(field, ReferenceContentFieldSettings.ShowAncestors, true);
        }
        else if (fieldType == CmsReferenceFieldOption)
        {
            SetSetting(
                field,
                ReferenceContentFieldSettings.TargetKind,
                ReferenceContentFieldSettings.TargetKindCmsDocument);
            field.Settings[ReferenceContentFieldSettings.AllowedSources] =
                JsonSerializer.SerializeToElement(
                    CmsContentReferenceSources.All.ToArray());
        }
        else if (fieldType == ContentFieldTypes.List)
        {
            SetSetting(field, CompositeContentFieldSettings.ItemType, CompositeContentFieldSettings.Text);
            SetSetting(field, CompositeContentFieldSettings.MinimumItems, 0);
            SetSetting(field, CompositeContentFieldSettings.MaximumItems, 5);
            SetAllowedValuesText(field, string.Empty);
        }
        else if (fieldType == ContentFieldTypes.Range)
        {
            SetSetting(field, RangeContentFieldSettings.Start, 1);
            SetSetting(field, RangeContentFieldSettings.End, 10);
            SetSetting(field, RangeContentFieldSettings.AllowNegative, false);
        }
        else if (fieldType == ContentFieldTypes.Gallery)
        {
            SetSetting(field, CompositeContentFieldSettings.MaximumItems, 12);
        }
        else if (fieldType == ContentFieldTypes.Dictionary)
        {
            SetSetting(field, CompositeContentFieldSettings.ValueType, CompositeContentFieldSettings.Text);
            SetSetting(field, CompositeContentFieldSettings.MaximumEntries, 10);
        }

        Fields.Add(field);

        _selectedFieldIndex = Fields.Count - 1;
    }

    private void SelectField(int index)
    {
        _selectedFieldIndex = index;
    }

    private void OnSelectedFieldChanged(ContentFieldDefinition field)
    {
        field.Name = CreateUniqueFieldName(
            GenerateHandle(field.Name),
            field);
    }

    private async Task OpenFieldSettingsDialogAsync(int index)
    {
        if (index < 0 || index >= Fields.Count)
        {
            return;
        }

        var clone = CloneField(Fields[index]);
        var ownerFields = Fields
            .Select((field, fieldIndex) => fieldIndex == index ? clone : field)
            .ToList();
        var option = GetFieldOption(clone);
        var result = await DialogService.OpenAsync<ContentFieldSettingsDialog>(
            L["Edit {0}", FieldLabel(clone)],
            new Dictionary<string, object?>
            {
                [nameof(ContentFieldSettingsDialog.Field)] = clone,
                [nameof(ContentFieldSettingsDialog.OwnerFields)] = ownerFields,
                [nameof(ContentFieldSettingsDialog.ContentTypes)] = _availableParentContentTypes,
                [nameof(ContentFieldSettingsDialog.CurrentContentTypeAlias)] = AliasValue,
                [nameof(ContentFieldSettingsDialog.FieldTypeLabel)] = option.Label
            },
            new DialogOptions
            {
                Width = "740px",
                Resizable = true,
                Draggable = false,
                CloseDialogOnOverlayClick = false
            });

        if (result is not ContentFieldDefinition updated)
        {
            return;
        }

        updated.Name = CreateUniqueFieldName(
            GenerateHandle(updated.Name),
            Fields[index]);
        Fields[index] = updated;
        _selectedFieldIndex = index;
    }

    private void MoveField(int index, int direction)
    {
        var target = index + direction;
        if (target < 0 || target >= Fields.Count) return;

        (Fields[index], Fields[target]) = (Fields[target], Fields[index]);
        _selectedFieldIndex = target;
    }

    private void DuplicateField(int index)
    {
        if (index < 0 || index >= Fields.Count) return;

        var clone = CloneField(Fields[index]);
        clone.Label = $"{FieldLabel(clone)} copy";
        clone.Name = CreateUniqueFieldName($"{clone.Name}-copy");
        Fields.Insert(index + 1, clone);
        _selectedFieldIndex = index + 1;
    }

    private void RemoveField(int index)
    {
        if (index < 0 || index >= Fields.Count) return;

        Fields.RemoveAt(index);
        if (_selectedFieldIndex == index)
        {
            _selectedFieldIndex = null;
        }
        else if (_selectedFieldIndex > index)
        {
            _selectedFieldIndex--;
        }
    }

    private void UpdateSelectedLabel(string? value)
    {
        if (SelectedField is null) return;

        SelectedField.Label = value;
        if (string.IsNullOrWhiteSpace(SelectedField.Name) || SelectedField.Name.StartsWith("new-field", StringComparison.OrdinalIgnoreCase))
        {
            SelectedField.Name = CreateUniqueFieldName(GenerateHandle(value ?? string.Empty));
        }
    }

    private void UpdateSelectedName(string? value)
    {
        if (SelectedField is null) return;
        SelectedField.Name = CreateUniqueFieldName(GenerateHandle(value ?? string.Empty), SelectedField);
    }

    private FieldTypeOption GetFieldOption(string fieldType)
        => FieldOptions.FirstOrDefault(option => option.Value == fieldType)
            ?? FieldOptions[0];

    private FieldTypeOption GetFieldOption(ContentFieldDefinition field) =>
        IsCmsDocumentReference(field)
            ? GetFieldOption(CmsReferenceFieldOption)
            : IsHierarchyReference(field)
                ? GetFieldOption(HierarchyReferenceFieldOption)
                : GetFieldOption(field.FieldType);

    private static bool IsCmsDocumentReference(ContentFieldDefinition field) =>
        string.Equals(
            GetSettingString(field, ReferenceContentFieldSettings.TargetKind),
            ReferenceContentFieldSettings.TargetKindCmsDocument,
            StringComparison.Ordinal);

    private static string FieldLabel(ContentFieldDefinition field)
        => string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label!;

    private int? GetIntSetting(ContentFieldDefinition field, string key)
        => TryGetSetting(field, key, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private decimal? GetDecimalSetting(ContentFieldDefinition field, string key)
        => TryGetSetting(field, key, out var value) && value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private string? GetStringSetting(ContentFieldDefinition field, string key)
    {
        if (!TryGetSetting(field, key, out var value)) return null;
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private async Task OnReferenceTargetChangedAsync(
        ContentFieldDefinition field,
        string? targetContentTypeId)
    {
        SetSetting(
            field,
            ReferenceContentFieldSettings.TargetContentTypeId,
            targetContentTypeId);
        field.Indexed = true;
        field.Settings.Remove(
            ReferenceContentFieldSettings.TargetFilterField);
        if (TryParseContentTypeId(targetContentTypeId, out var targetId))
        {
            await LoadReferenceTargetDefinitionAsync(targetId);
        }
    }

    private void OnReferenceDependencyChanged(
        ContentFieldDefinition field,
        string? dependencyName)
    {
        SetSetting(
            field,
            ReferenceContentFieldSettings.DependsOnField,
            dependencyName);
        field.Settings.Remove(
            ReferenceContentFieldSettings.TargetFilterField);
    }

    private IEnumerable<ContentFieldDefinition> DependencyReferenceFields(
        ContentFieldDefinition selected)
    {
        var selectedIndex = Fields.IndexOf(selected);
        return Fields
            .Take(Math.Max(0, selectedIndex))
            .Where(field => field.FieldType == ContentFieldTypes.Reference);
    }

    private IEnumerable<ContentFieldDefinition> TargetReferenceFields(
        ContentFieldDefinition selected)
    {
        var targetIdText = GetStringSetting(
            selected,
            ReferenceContentFieldSettings.TargetContentTypeId);
        if (!TryParseContentTypeId(targetIdText, out var targetId))
        {
            return [];
        }

        if (_referenceTargetDefinitions.TryGetValue(targetId, out var target))
        {
            return target.Fields.Where(field => field.FieldType == ContentFieldTypes.Reference);
        }
        return [];
    }

    private async Task LoadReferenceTargetDefinitionsAsync()
    {
        foreach (var targetId in Fields
                     .Where(field =>
                         field.FieldType == ContentFieldTypes.Reference)
                     .Select(field => GetStringSetting(
                         field,
                         ReferenceContentFieldSettings.TargetContentTypeId))
                     .Select(value => TryParseContentTypeId(value, out var id) ? id : 0)
                     .Where(id => id > 0)
                     .Distinct())
        {
            await LoadReferenceTargetDefinitionAsync(targetId);
        }
    }

    private async Task LoadReferenceTargetDefinitionAsync(long targetId)
    {
        if (_referenceTargetDefinitions.ContainsKey(targetId))
        {
            return;
        }

        var result = await ContentTypesApi.GetByIdAsync(targetId);
        if (result is Result<ContentTypeDetail, AeroError>.Ok ok)
        {
            _referenceTargetDefinitions[targetId] = ok.Value;
        }
    }

    private static bool TryParseContentTypeId(string? value, out long id) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private static bool SupportsFullTextSearch(
        ContentFieldDefinition field) =>
        field.FieldType is
            ContentFieldTypes.Text or
            ContentFieldTypes.RichText or
            ContentFieldTypes.Url or
            ContentFieldTypes.List or
            ContentFieldTypes.Dictionary;

    private static bool SupportsSemanticSearch(
        ContentFieldDefinition field) =>
        field.FieldType is
            ContentFieldTypes.Text or
            ContentFieldTypes.RichText;

    private static bool GetBoolSetting(
        ContentFieldDefinition field,
        string key,
        bool fallback = false) =>
        TryGetSetting(field, key, out var value)
            ? value.ValueKind == JsonValueKind.True
            : fallback;

    private string GetAllowedValuesText(ContentFieldDefinition field)
    {
        if (!TryGetSetting(field, CompositeContentFieldSettings.AllowedValues, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine,
            value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static void SetAllowedValuesText(ContentFieldDefinition field, string? value)
    {
        var values = (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        field.Settings[CompositeContentFieldSettings.AllowedValues] = JsonSerializer.SerializeToElement(
            values,
            ContentJsonContext.Default.ListString);
    }

    private static bool IsCompositeField(ContentFieldDefinition field) =>
        field.FieldType is ContentFieldTypes.List or ContentFieldTypes.Gallery or ContentFieldTypes.Dictionary;

    private int GetRangeStartMinimum(ContentFieldDefinition field) =>
        GetBoolSetting(field, RangeContentFieldSettings.AllowNegative)
            ? int.MinValue
            : 0;

    private static bool IsListOptional(ContentFieldDefinition field) =>
        !field.Required
        && (!field.Settings.TryGetValue(
                CompositeContentFieldSettings.MinimumItems,
                out var minimum)
            || !minimum.TryGetInt32(out var minimumValue)
            || minimumValue == 0);

    private static void SetListOptional(
        ContentFieldDefinition field,
        bool optional)
    {
        field.Required = !optional;
        var currentMinimum =
            field.Settings.TryGetValue(
                CompositeContentFieldSettings.MinimumItems,
                out var minimum)
            && minimum.TryGetInt32(out var parsed)
                ? parsed
                : 0;
        SetSetting(
            field,
            CompositeContentFieldSettings.MinimumItems,
            optional ? 0 : Math.Max(1, currentMinimum));
    }

    private static void SetRangeAllowNegative(
        ContentFieldDefinition field,
        bool allowNegative)
    {
        SetSetting(
            field,
            RangeContentFieldSettings.AllowNegative,
            allowNegative);
        if (allowNegative)
        {
            return;
        }

        if (field.Settings.TryGetValue(
                RangeContentFieldSettings.Start,
                out var start)
            && start.TryGetInt32(out var startValue)
            && startValue < 0)
        {
            SetSetting(field, RangeContentFieldSettings.Start, 0);
        }

        if (field.Settings.TryGetValue(
                RangeContentFieldSettings.End,
                out var end)
            && end.TryGetInt32(out var endValue)
            && endValue < 0)
        {
            SetSetting(field, RangeContentFieldSettings.End, 0);
        }
    }

    private static bool TryGetSetting(ContentFieldDefinition field, string key, out JsonElement value)
        => field.Settings.TryGetValue(key, out value);

    private static void SetSetting(ContentFieldDefinition field, string key, int? value)
    {
        if (value is null)
        {
            field.Settings.Remove(key);
            return;
        }

        field.Settings[key] = JsonSerializer.SerializeToElement(
            value.Value,
            ContentJsonContext.Default.Int32);
    }

    private static void SetSetting(ContentFieldDefinition field, string key, decimal? value)
    {
        if (value is null)
        {
            field.Settings.Remove(key);
            return;
        }

        field.Settings[key] = JsonSerializer.SerializeToElement(
            value.Value,
            ContentJsonContext.Default.Decimal);
    }

    private static void SetSetting(ContentFieldDefinition field, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            field.Settings.Remove(key);
            return;
        }

        field.Settings[key] = JsonSerializer.SerializeToElement(
            value,
            ContentJsonContext.Default.String);
    }

    private static void SetSetting(ContentFieldDefinition field, string key, bool value)
    {
        field.Settings[key] = JsonSerializer.SerializeToElement(
            value,
            ContentJsonContext.Default.Boolean);
    }

    private static bool IsHierarchyReference(ContentFieldDefinition field) =>
        field.FieldType == ContentFieldTypes.Reference
        && string.Equals(
            GetSettingString(field, ReferenceContentFieldSettings.SelectionMode),
            ReferenceContentFieldSettings.SelectionModeHierarchy,
            StringComparison.Ordinal);

    private static string? GetSettingString(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private StandaloneEditorConstructionOptions ScribanEditorConstructionOptions(StandaloneCodeEditor editor)
        => new()
        {
            AutomaticLayout = true,
            Language = "liquid",
            Value = ScribanTemplate ?? string.Empty,
            Minimap = new EditorMinimapOptions { Enabled = false },
            ScrollBeyondLastLine = false,
            WordWrap = "on",
            LineNumbers = "on",
            TabSize = 2
        };

    private async Task OnScribanEditorContentChanged()
    {
        if (_scribanEditor is not null)
        {
            ScribanTemplate = await _scribanEditor.GetValue();
        }
    }

    private async Task AutoGenerateTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<article class="content-type-{AliasValue}">""");
        foreach (var field in Fields)
        {
            sb.AppendLine($"""  <section class="aero-field aero-field-{field.FieldType}">""");
            sb.AppendLine($"    {{{{ {ScribanFieldAccessor(field.Name)} }}}}");
            sb.AppendLine("  </section>");
        }
        sb.AppendLine("</article>");
        ScribanTemplate = sb.ToString();
        if (_scribanEditor is not null)
        {
            await _scribanEditor.SetValue(ScribanTemplate);
        }
    }

    private static string ScribanFieldAccessor(string fieldName) =>
        Regex.IsMatch(fieldName, "^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)
            ? $"fields.{fieldName}"
            : $"fields[\"{fieldName.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"]";

    private async Task SaveAsync()
    {
        if (_useCustomTemplate && _scribanEditor is not null)
        {
            ScribanTemplate = await _scribanEditor.GetValue();
        }

        if (!ValidateBeforeSave()) return;

        _isSaving = true;
        try
        {
            var request = new CreateContentTypeRequest(
                AliasValue,
                Name.Trim(),
                Description,
                Category,
                null,
                AllowPublicUrl,
                IncludeInSearch,
                IncludeInPublicAi,
                Fields,
                _useCustomTemplate ? ScribanTemplate : null,
                null,
                Cardinality,
                Structure,
                new ContentHierarchyRules
                {
                    AllowRootItems = AllowRootItems,
                    MaximumDepth = MaximumHierarchyDepth,
                    RequireSameTypeParent = RequireSameTypeParent,
                    AllowedParentContentTypeIds = AllowedParentContentTypeIds,
                    DefaultOrdering = HierarchyOrdering
                });

            var result = IsNew
                ? await ContentTypesApi.CreateAsync(request)
                : await ContentTypesApi.UpdateAsync(Alias!, request);

            if (result is Result<ContentTypeDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Save failed", FormatError(failure.Error));
                return;
            }

            Notify(NotificationSeverity.Success, "Saved", $"{Name} is ready for entries.");
            Navigation.NavigateTo("/manager/content-types");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private bool ValidateBeforeSave()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Notify(NotificationSeverity.Warning, "Missing name", "Give this content type a name.");
            _activeTab = EditorTab.Basics;
            return false;
        }

        if (string.IsNullOrWhiteSpace(AliasValue))
        {
            AliasValue = GenerateHandle(Name);
        }

        if (Fields.Count == 0)
        {
            Notify(NotificationSeverity.Warning, "No fields", "Add at least one field so editors have something to fill out.");
            _activeTab = EditorTab.Fields;
            return false;
        }

        if (Structure == ContentStructure.Hierarchical
            && Cardinality == ContentCardinality.Singleton)
        {
            Notify(
                NotificationSeverity.Warning,
                "Invalid entry organization",
                "Hierarchical content types must use collection cardinality.");
            _activeTab = EditorTab.Basics;
            return false;
        }

        if (Structure == ContentStructure.Hierarchical
            && MaximumHierarchyDepth is < 1 or > MaximumHierarchyDepthLimit)
        {
            Notify(
                NotificationSeverity.Warning,
                "Invalid hierarchy depth",
                $"Hierarchy depth must be between 1 and {MaximumHierarchyDepthLimit}.");
            _activeTab = EditorTab.Basics;
            return false;
        }

        var duplicate = Fields
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            Notify(NotificationSeverity.Warning, "Duplicate field handle", $"'{duplicate.Key}' is used more than once.");
            _activeTab = EditorTab.Fields;
            return false;
        }

        foreach (var field in Fields.Where(IsCompositeField))
        {
            if (!string.IsNullOrWhiteSpace(field.DefaultValue))
            {
                Notify(NotificationSeverity.Warning, "Unsupported default value", $"{FieldLabel(field)} does not support a scalar default value.");
                _activeTab = EditorTab.Fields;
                return false;
            }

            if (field.FieldType == ContentFieldTypes.List)
            {
                var allowed = GetAllowedValuesText(field)
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowed.Length == 0)
                {
                    Notify(NotificationSeverity.Warning, "Missing allowed values", $"Add at least one allowed value for {FieldLabel(field)}.");
                    _activeTab = EditorTab.Fields;
                    return false;
                }
                var effectiveChoiceCount = allowed.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if (GetStringSetting(field, CompositeContentFieldSettings.ItemType) == CompositeContentFieldSettings.Number)
                {
                    var numbers = new List<decimal>(allowed.Length);
                    foreach (var item in allowed)
                    {
                        if (!decimal.TryParse(item, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                        {
                            Notify(NotificationSeverity.Warning, "Invalid number", $"Every allowed value for {FieldLabel(field)} must be an invariant number.");
                            _activeTab = EditorTab.Fields;
                            return false;
                        }

                        numbers.Add(number);
                    }

                    effectiveChoiceCount = numbers.Distinct().Count();
                }

                if (effectiveChoiceCount != allowed.Length)
                {
                    Notify(NotificationSeverity.Warning, "Duplicate allowed values", $"Every allowed value for {FieldLabel(field)} must be unique.");
                    _activeTab = EditorTab.Fields;
                    return false;
                }
                var minimum = GetIntSetting(field, CompositeContentFieldSettings.MinimumItems) ?? 0;
                if (minimum > effectiveChoiceCount)
                {
                    Notify(NotificationSeverity.Warning, "Impossible minimum", $"{FieldLabel(field)} cannot require more selections than its {effectiveChoiceCount} unique choices.");
                    _activeTab = EditorTab.Fields;
                    return false;
                }
            }
        }

        foreach (var field in Fields.Where(
                     field => field.FieldType == ContentFieldTypes.Reference))
        {
            if (IsCmsDocumentReference(field))
            {
                if (!field.Settings.TryGetValue(
                        ReferenceContentFieldSettings.AllowedSources,
                        out var allowedSources)
                    || allowedSources.ValueKind != JsonValueKind.Array
                    || allowedSources.GetArrayLength() == 0)
                {
                    Notify(
                        NotificationSeverity.Warning,
                        "Missing content source",
                        $"Choose at least one site content source for {FieldLabel(field)}.");
                    _activeTab = EditorTab.Fields;
                    return false;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    GetSettingString(
                        field,
                        ReferenceContentFieldSettings.TargetContentTypeId)))
            {
                Notify(
                    NotificationSeverity.Warning,
                    "Missing content type",
                    $"Choose the content type linked by {FieldLabel(field)}.");
                _activeTab = EditorTab.Fields;
                return false;
            }

            var dependsOn = GetSettingString(
                field,
                ReferenceContentFieldSettings.DependsOnField);
            var targetFilter = GetSettingString(
                field,
                ReferenceContentFieldSettings.TargetFilterField);
            if (string.IsNullOrWhiteSpace(dependsOn)
                != string.IsNullOrWhiteSpace(targetFilter))
            {
                Notify(
                    NotificationSeverity.Warning,
                    "Incomplete cascading reference",
                    $"Choose both the dependent field and target relationship field for {FieldLabel(field)}, or clear both.");
                _activeTab = EditorTab.Fields;
                return false;
            }
        }

        foreach (var field in Fields.Where(
                     field => field.FieldType == ContentFieldTypes.Range))
        {
            var start = GetIntSetting(field, RangeContentFieldSettings.Start);
            var end = GetIntSetting(field, RangeContentFieldSettings.End);
            if (start is null || end is null)
            {
                Notify(
                    NotificationSeverity.Warning,
                    "Missing range",
                    $"Set both Start with and End with for {FieldLabel(field)}.");
                _activeTab = EditorTab.Fields;
                return false;
            }

            if (start > end)
            {
                Notify(
                    NotificationSeverity.Warning,
                    "Invalid range",
                    $"{FieldLabel(field)} must start at or below its ending value.");
                _activeTab = EditorTab.Fields;
                return false;
            }

            if (!GetBoolSetting(
                    field,
                    RangeContentFieldSettings.AllowNegative)
                && start < 0)
            {
                Notify(
                    NotificationSeverity.Warning,
                    "Negative range",
                    $"Enable negative values or start {FieldLabel(field)} at zero or higher.");
                _activeTab = EditorTab.Fields;
                return false;
            }
        }

        return true;
    }

    private static string FormatError(AeroError error) => error switch
    {
        AeroError.Validation validation => string.Join("; ", validation.Errors),
        AeroError.Error value => value.msg,
        AeroError.NotFound value => value.msg,
        AeroError.Conflict value => value.msg,
        AeroError.BadRequest value => value.msg,
        AeroError.InvalidRequest value => value.msg,
        AeroError.HttpRequest value => value.msg ?? "The request failed.",
        _ => error.ToString()
    };

    private void Cancel()
        => Navigation.NavigateTo("/manager/content-types");

    private async Task SwitchToEntriesTab()
    {
        _activeTab = EditorTab.Entries;
        // Defer reload to next render cycle — _entriesGrid is null until
        // the conditional Entries panel actually renders on the UI thread.
        await Task.Yield();
        if (_entriesGrid is not null)
        {
            await _entriesGrid.Reload();
        }
    }

    private async Task LoadEntriesAsync(LoadDataArgs args)
    {
        if (string.IsNullOrWhiteSpace(AliasValue))
        {
            _entries = [];
            _entriesCount = 0;
            return;
        }

        _entriesLoading = true;
        try
        {
            var result = await ContentItemsApi.GetAllAsync(AliasValue, args.Skip ?? 0, args.Top ?? 10, _entriesSearchText);
            if (result is Result<PagedResult<ContentItemSummary>, AeroError>.Ok ok)
            {
                _entries = ok.Value.Items;
                _entriesCount = (int)ok.Value.TotalCount;
                return;
            }

            if (result is Result<PagedResult<ContentItemSummary>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Entries failed to load", failure.Error.ToString());
            }

            _entries = [];
            _entriesCount = 0;
        }
        finally
        {
            _entriesLoading = false;
        }
    }

    private async Task OnEntriesSearchChanged(string value)
    {
        _entriesSearchText = value;
        if (_entriesGrid is not null)
        {
            await _entriesGrid.FirstPage();
        }
    }

    private void CreateEntry()
        => Navigation.NavigateTo($"/manager/content/{AliasValue}/editor");

    private void EditEntry(long id)
        => Navigation.NavigateTo($"/manager/content/{AliasValue}/editor/{id}");

    private void OnEntryRowClick(DataGridRowMouseEventArgs<ContentItemSummary> args)
    {
        if (args.Data is not null)
        {
            EditEntry(args.Data.Id);
        }
    }

    private bool CanOpenPublishedPage(ContentItemSummary item)
        => AllowPublicUrl &&
           string.Equals(item.PublicationState, "Published", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(AliasValue) &&
           !string.IsNullOrWhiteSpace(item.Slug);

    private string BuildPublicContentPath(string slug)
        => $"/content/{Uri.EscapeDataString(AliasValue.Trim())}/{Uri.EscapeDataString(slug.Trim())}";

    private string BuildPublicContentUrl(string slug)
        => new Uri(new Uri(Navigation.BaseUri), BuildPublicContentPath(slug).TrimStart('/')).ToString();

    private async Task DeleteEntryAsync(long id)
    {
        var confirmed = await DialogService.Confirm(
            "Delete this entry? This cannot be undone.",
            "Delete Entry",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true) return;

        var result = await ContentItemsApi.DeleteAsync(AliasValue, id);
        if (result is Result<bool, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Deleted", "Entry removed.");
        if (_entriesGrid is not null)
        {
            await _entriesGrid.Reload();
        }
    }

    private async Task PublishEntryAsync(long id)
    {
        var result = await ContentItemsApi.PublishAsync(AliasValue, id);
        if (result is Result<ContentItemDetail, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Publish failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Published", "Entry is live.");
        if (_entriesGrid is not null)
        {
            await _entriesGrid.Reload();
        }
    }

    private async Task UnpublishEntryAsync(long id)
    {
        var result = await ContentItemsApi.UnpublishAsync(AliasValue, id);
        if (result is Result<ContentItemDetail, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Unpublish failed", failure.Error.ToString());
            return;
        }

        Notify(NotificationSeverity.Success, "Unpublished", "Entry returned to draft.");
        if (_entriesGrid is not null)
        {
            await _entriesGrid.Reload();
        }
    }

    private static string FormatEntryDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("MMM d, yyyy") ?? "-";

    private static string FormatEntryCulture(string? culture)
        => string.IsNullOrWhiteSpace(culture) ? "Default" : culture.Trim();

    private static string FormatFirstField(string value)
    {
        var trimmed = value.Trim('"');
        return trimmed.Length <= 96 ? trimmed : $"{trimmed[..96]}...";
    }

    private string CreateUniqueFieldName(string baseName, ContentFieldDefinition? current = null)
    {
        var normalized = string.IsNullOrWhiteSpace(baseName) ? "new-field" : GenerateHandle(baseName);
        var candidate = normalized;
        var suffix = 2;
        while (Fields.Any(field => !ReferenceEquals(field, current) && string.Equals(field.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{normalized}-{suffix++}";
        }

        return candidate;
    }

    private static ContentFieldDefinition CloneField(ContentFieldDefinition field)
        => new()
        {
            Name = field.Name,
            FieldType = field.FieldType,
            Label = field.Label,
            Required = field.Required,
            DefaultValue = field.DefaultValue,
            Placeholder = field.Placeholder,
            Indexed = field.Indexed,
            FullTextSearchable = field.FullTextSearchable,
            SemanticSearchable = field.SemanticSearchable,
            AiExposure = field.AiExposure,
            Settings = field.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.Clone())
        };

    private static string GenerateHandle(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9\s_-]", "");
        slug = Regex.Replace(slug, @"[\s_]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "content-type" : slug;
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });

    private enum EditorTab
    {
        Basics,
        Fields,
        Display,
        Entries
    }

    private sealed record DropDownItem(string Text, string Value);

    private sealed record FieldTypeOption(string Value, string Label, string Icon, string Description);
}
