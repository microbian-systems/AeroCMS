using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Aero.Cms.Abstractions.Media;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Shared.Components;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

/// <summary>
/// Represents a class for ContentItemEditor.
/// </summary>
public partial class ContentItemEditor
{
        /// <summary>
    /// Gets or sets the Alias.
    /// </summary>
[Parameter] public string Alias { get; set; } = string.Empty;
        /// <summary>
    /// Gets or sets the Id.
    /// </summary>
[Parameter] public long? Id { get; set; }
        /// <summary>
    /// Gets or sets the Requested Tab.
    /// </summary>
[SupplyParameterFromQuery(Name = "tab")] public string? RequestedTab { get; set; }
    /// <summary>Optional hierarchy parent preselected when creating a child from the tree.</summary>
    [SupplyParameterFromQuery(Name = "parentId")] public long? RequestedParentId { get; set; }

    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private IContentItemsHttpClient ContentItemsApi { get; set; } = default!;
    [Inject] private ISitesHttpClient SitesClient { get; set; } = default!;
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IStringLocalizer<Aero.Cms.Shared.Localization.ManagerResource> L { get; set; } = default!;

    private ContentTypeDetail? _typeDefinition;
    private readonly Dictionary<string, string> _fieldValues = [];
    private readonly Dictionary<string, decimal?> _numberValues = [];
    private readonly Dictionary<string, int?> _rangeValues = [];
    private readonly Dictionary<string, bool> _boolValues = [];
    private readonly Dictionary<string, DateTime?> _dateValues = [];
    private readonly Dictionary<string, List<string>> _listValues = [];
    private readonly Dictionary<string, List<string>> _galleryValues = [];
    private readonly Dictionary<string, List<KeyValueEditorRow>> _dictionaryValues = [];
    private readonly Dictionary<string, string> _fieldErrors = [];

    private IReadOnlyList<ContentItemDetail> _cultureVariants = [];
    private SiteViewModel? _currentSite;
    private bool _isSaving;
    private bool _isLoadingTranslations;
    private bool _slugLocked;
    private bool _allowPublicUrl;
    private bool _showPublishedPagePreview;
    private bool _showMediaSelector;
    private string? _activeMediaField;
    private string? _activeGalleryField;
    private string _typeName = "Content";
    private string _title = string.Empty;
    private string _slug = string.Empty;
    private string _publicationState = "Draft";
    private DateTimeOffset? _publishedOn;
    private int _versionNumber;
    private string _activeTab = "content";
    private string _culture = string.Empty;
    private long? _translationGroupId;
    private long? _sourceItemId;
    private long? _parentId;
    private int _sortOrder;
    private IReadOnlyList<ContentParentOption> _parentOptions = [];

    private static bool IsHierarchyReference(ContentFieldDefinition field) =>
        field.FieldType == ContentFieldTypes.Reference
        && string.Equals(
            GetStringSetting(field, ReferenceContentFieldSettings.SelectionMode),
            ReferenceContentFieldSettings.SelectionModeHierarchy,
            StringComparison.Ordinal);

    private static bool GetBoolSetting(
        ContentFieldDefinition field,
        string key,
        bool fallback = false) =>
        field.Settings.TryGetValue(key, out var value)
            ? value.ValueKind == JsonValueKind.True
            : fallback;

    private string? GetDependentReferenceValue(ContentFieldDefinition field)
    {
        var dependencyName = GetStringSetting(
            field,
            ReferenceContentFieldSettings.DependsOnField);
        return !string.IsNullOrWhiteSpace(dependencyName)
            && _fieldValues.TryGetValue(dependencyName, out var value)
                ? value
                : null;
    }

    private string GetDependencyLabel(ContentFieldDefinition field)
    {
        var dependencyName = GetStringSetting(
            field,
            ReferenceContentFieldSettings.DependsOnField);
        var dependency = _typeDefinition?.Fields.FirstOrDefault(
            candidate => string.Equals(
                candidate.Name,
                dependencyName,
                StringComparison.Ordinal));
        return dependency is null
            ? "the parent selection"
            : FieldLabel(dependency);
    }

    private async Task OnReferenceValueChangedAsync(
        ContentFieldDefinition field,
        string? value)
    {
        _fieldValues[field.Name] = value ?? string.Empty;
        ClearDependentReferences(field.Name);
        await InvokeAsync(StateHasChanged);
    }

    private void ClearDependentReferences(string changedFieldName)
    {
        if (_typeDefinition is null)
        {
            return;
        }

        var pending = new Queue<string>();
        pending.Enqueue(changedFieldName);
        while (pending.TryDequeue(out var dependencyName))
        {
            foreach (var dependent in _typeDefinition.Fields.Where(
                         candidate => candidate.FieldType == ContentFieldTypes.Reference
                             && string.Equals(
                                 GetStringSetting(
                                     candidate,
                                     ReferenceContentFieldSettings.DependsOnField),
                                 dependencyName,
                                 StringComparison.Ordinal)))
            {
                if (_fieldValues.TryGetValue(dependent.Name, out var current)
                    && !string.IsNullOrWhiteSpace(current))
                {
                    _fieldValues[dependent.Name] = string.Empty;
                }

                pending.Enqueue(dependent.Name);
            }
        }
    }

    private IReadOnlyList<string> SupportedCultures =>
        _currentSite?.SupportedCultures is { Count: > 0 } cultures
            ? cultures
            : [_currentSite?.DefaultCulture ?? "en-US"];

    private IEnumerable<string> AvailableTranslationCultures =>
        SupportedCultures
            .Where(culture => !string.Equals(NormalizeCulture(culture), NormalizeCulture(_culture), StringComparison.OrdinalIgnoreCase))
            .Where(culture => _cultureVariants.All(variant => !string.Equals(NormalizeCulture(variant.Culture), NormalizeCulture(culture), StringComparison.OrdinalIgnoreCase)));

    private BadgeStyle StatusBadgeStyle => _publicationState == "Published"
        ? BadgeStyle.Success
        : BadgeStyle.Info;

    private bool CanPreviewPublishedPage =>
        Id.HasValue &&
        _allowPublicUrl &&
        string.Equals(_publicationState, "Published", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_slug);

    private string PublicPath => $"/content/{Alias}/{_slug}";

    private string? FrameUrl => CanPreviewPublishedPage
        ? new Uri(new Uri(Navigation.BaseUri), PublicPath.TrimStart('/')).ToString()
        : null;

        /// <summary>
    /// OnInitializedAsync method.
    /// </summary>
protected override async Task OnInitializedAsync()
    {
        _activeTab = string.Equals(RequestedTab, "translations", StringComparison.OrdinalIgnoreCase)
            ? "translations"
            : "content";

        await LoadCurrentSiteAsync();

        var typeResult = await ContentTypesApi.GetByAliasAsync(Alias);
        if (typeResult is not Result<ContentTypeDetail, AeroError>.Ok typeOk)
        {
            Notify(NotificationSeverity.Error, "Missing content type", "The selected content type could not be loaded.");
            Navigation.NavigateTo("/manager/content-types");
            return;
        }

        _typeDefinition = typeOk.Value;
        _typeName = _typeDefinition.Name;
        _allowPublicUrl = _typeDefinition.AllowPublicUrl;
        _culture = _currentSite?.DefaultCulture
            ?? System.Globalization.CultureInfo.CurrentUICulture.Name;
        InitializeFieldDictionaries();

        if (Id.HasValue)
        {
            var itemResult = await ContentItemsApi.GetByIdAsync(Alias, Id.Value);
            if (itemResult is Result<ContentItemDetail, AeroError>.Ok itemOk)
            {
                LoadItem(itemOk.Value);
                await LoadTranslationsAsync();
            }
            else if (itemResult is Result<ContentItemDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Load failed", failure.Error.ToString());
            }
        }
        else if (RequestedParentId is > 0)
        {
            _parentId = RequestedParentId;
        }

        await LoadParentOptionsAsync();
    }

    private async Task LoadCurrentSiteAsync()
    {
        var result = await SitesClient.GetDefaultAsync();
        if (result is Result<SiteViewModel, AeroError>.Ok ok)
        {
            _currentSite = ok.Value;
        }
    }

    private void InitializeFieldDictionaries()
    {
        if (_typeDefinition is null) return;

        foreach (var field in _typeDefinition.Fields)
        {
            switch (field.FieldType)
            {
                case "number":
                    _numberValues.TryAdd(field.Name, TryParseDecimal(field.DefaultValue));
                    break;
                case ContentFieldTypes.Range:
                    _rangeValues.TryAdd(
                        field.Name,
                        int.TryParse(
                            field.DefaultValue,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var rangeValue)
                            ? rangeValue
                            : null);
                    break;
                case "boolean":
                    _boolValues.TryAdd(field.Name, bool.TryParse(field.DefaultValue, out var value) && value);
                    break;
                case "date":
                    _dateValues.TryAdd(field.Name, DateTime.TryParse(field.DefaultValue, out var date) ? date : null);
                    break;
                case ContentFieldTypes.List:
                    _listValues.TryAdd(field.Name, []);
                    break;
                case ContentFieldTypes.Gallery:
                    _galleryValues.TryAdd(field.Name, []);
                    break;
                case ContentFieldTypes.Dictionary:
                    _dictionaryValues.TryAdd(field.Name, []);
                    break;
                default:
                    _fieldValues.TryAdd(field.Name, field.DefaultValue ?? string.Empty);
                    break;
            }
        }
    }

    private void LoadItem(ContentItemDetail detail)
    {
        _title = detail.Title;
        _slug = detail.Slug;
        _slugLocked = true;
        _publicationState = detail.PublicationState;
        _publishedOn = detail.PublishedOn;
        _versionNumber = detail.VersionNumber;
        _culture = detail.Culture;
        _translationGroupId = detail.TranslationGroupId;
        _sourceItemId = detail.SourceItemId;
        _parentId = detail.ParentId;
        _sortOrder = detail.SortOrder;
        PopulateFieldValues(detail.Fields);
    }

    private async Task LoadParentOptionsAsync()
    {
        if (_typeDefinition?.Structure != ContentStructure.Hierarchical)
        {
            _parentOptions = [];
            return;
        }

        var result = await ContentItemsApi.GetHierarchyAsync(Alias, _culture);
        if (result is Result<ContentHierarchyTreeResult, AeroError>.Ok ok)
        {
            var blockedIds = Id is { } currentId
                ? FindSubtreeIds(ok.Value.Roots, currentId)
                : new HashSet<long>();
            _parentOptions = FlattenParentOptions(ok.Value.Roots)
                .Where(option => !blockedIds.Contains(option.Id))
                .Where(option => option.CanAcceptChildren)
                .ToArray();
        }
        else if (result is Result<ContentHierarchyTreeResult, AeroError>.Failure failure)
        {
            Notify(
                NotificationSeverity.Warning,
                "Hierarchy unavailable",
                failure.Error.ToString());
        }
    }

    private static IEnumerable<ContentParentOption> FlattenParentOptions(
        IEnumerable<ContentHierarchyTreeNode> nodes,
        string parentPath = "")
    {
        foreach (var node in nodes)
        {
            var path = string.IsNullOrWhiteSpace(parentPath)
                ? node.Title
                : $"{parentPath} / {node.Title}";
            yield return new ContentParentOption(
                node.Id,
                node.Title,
                node.ContentTypeAlias,
                path,
                node.CanAcceptChildren);

            foreach (var child in FlattenParentOptions(node.Children, path))
            {
                yield return child;
            }
        }
    }

    private static HashSet<long> FindSubtreeIds(
        IEnumerable<ContentHierarchyTreeNode> roots,
        long currentId)
    {
        var blocked = new HashSet<long> { currentId };
        var current = FlattenNodes(roots).FirstOrDefault(node => node.Id == currentId);
        if (current is not null)
        {
            blocked.UnionWith(FlattenNodes(current.Children).Select(node => node.Id));
        }

        return blocked;
    }

    private static IEnumerable<ContentHierarchyTreeNode> FlattenNodes(
        IEnumerable<ContentHierarchyTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private void PopulateFieldValues(IReadOnlyDictionary<string, JsonElement> source)
    {
        if (_typeDefinition is null) return;

        foreach (var field in _typeDefinition.Fields)
        {
            if (!source.TryGetValue(field.Name, out var element)) continue;

            switch (field.FieldType)
            {
                case "number":
                    _numberValues[field.Name] = element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetDecimal();
                    break;
                case ContentFieldTypes.Range:
                    _rangeValues[field.Name] =
                        element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                        || !element.TryGetInt32(out var rangeValue)
                            ? null
                            : rangeValue;
                    break;
                case "boolean":
                    _boolValues[field.Name] = element.ValueKind == JsonValueKind.True;
                    break;
                case "date":
                    _dateValues[field.Name] = element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetDateTime();
                    break;
                case ContentFieldTypes.List:
                    var numericList = GetStringSetting(field, CompositeContentFieldSettings.ItemType) == CompositeContentFieldSettings.Number;
                    _listValues[field.Name] = element.ValueKind == JsonValueKind.Array
                        ? element.EnumerateArray()
                            .Select(item => NormalizeListValue(item, numericList))
                            .Where(item => item is not null)
                            .Select(item => item!)
                            .ToList()
                        : [];
                    break;
                case ContentFieldTypes.Gallery:
                    _galleryValues[field.Name] = element.ValueKind == JsonValueKind.Array
                        ? element.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList()
                        : [];
                    break;
                case ContentFieldTypes.Dictionary:
                    var numericDictionary = GetStringSetting(field, CompositeContentFieldSettings.ValueType) == CompositeContentFieldSettings.Number;
                    _dictionaryValues[field.Name] = element.ValueKind == JsonValueKind.Object
                        ? element.EnumerateObject()
                            .Select(property => new KeyValueEditorRow(
                                property.Name,
                                FormatDictionaryValue(property.Value, numericDictionary)))
                            .ToList()
                        : [];
                    break;
                default:
                    _fieldValues[field.Name] = element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? string.Empty : element.GetString() ?? string.Empty;
                    break;
            }
        }
    }

    private void OnTitleChanged(string value)
    {
        _title = value;
        if (!_slugLocked)
        {
            _slug = GenerateSlug(value);
        }
    }

    private void OnSlugChanged(string value)
    {
        _slug = GenerateSlug(value);
        _slugLocked = true;
    }

    private void SetDateValue(string fieldName, object? value)
    {
        _dateValues[fieldName] = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            _ => null
        };
    }

    private async Task SaveDraftAsync()
    {
        await SaveAsync(navigateAfterSave: true);
    }

    private async Task<ContentItemDetail?> SaveAsync(bool navigateAfterSave)
    {
        if (!ValidateForSave()) return null;

        _isSaving = true;
        try
        {
            var request = new CreateContentItemRequest(
                _title.Trim(),
                _allowPublicUrl ? _slug : GenerateSlug(_title),
                BuildFieldsDictionary(),
                null,
                null,
                _culture,
                _parentId,
                _sortOrder);

            var result = Id.HasValue
                ? await ContentItemsApi.UpdateAsync(Alias, Id.Value, request)
                : await ContentItemsApi.CreateAsync(Alias, request);

            if (result is Result<ContentItemDetail, AeroError>.Ok ok)
            {
                Id = ok.Value.Id;
                LoadItem(ok.Value);
                await LoadTranslationsAsync();
                Notify(NotificationSeverity.Success, "Saved", "Draft saved.");
                if (navigateAfterSave)
                {
                    Navigation.NavigateTo($"/manager/content/{Alias}");
                }

                return ok.Value;
            }

            if (result is Result<ContentItemDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Save failed", failure.Error.ToString());
            }

            return null;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task PublishAsync()
    {
        if (!ValidateForPublish()) return;

        var saved = await SaveAsync(navigateAfterSave: false);
        if (saved is null || !Id.HasValue) return;

        _isSaving = true;
        try
        {
            var result = await ContentItemsApi.PublishAsync(Alias, Id.Value);
            if (result is Result<ContentItemDetail, AeroError>.Ok ok)
            {
                LoadItem(ok.Value);
                Notify(NotificationSeverity.Success, "Published", "Entry published.");
                Navigation.NavigateTo($"/manager/content/{Alias}");
                return;
            }

            if (result is Result<ContentItemDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Publish failed", failure.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task UnpublishAsync()
    {
        if (!Id.HasValue) return;

        _isSaving = true;
        try
        {
            var result = await ContentItemsApi.UnpublishAsync(Alias, Id.Value);
            if (result is Result<ContentItemDetail, AeroError>.Ok ok)
            {
                LoadItem(ok.Value);
                Notify(NotificationSeverity.Success, "Unpublished", "Entry moved back to draft.");
                return;
            }

            if (result is Result<ContentItemDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Unpublish failed", failure.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (!Id.HasValue) return;

        var confirmed = await DialogService.Confirm(
            "Delete this entry? This cannot be undone.",
            "Delete Entry",
            new ConfirmOptions { OkButtonText = "Delete", CancelButtonText = "Cancel" });

        if (confirmed != true) return;

        _isSaving = true;
        try
        {
            var result = await ContentItemsApi.DeleteAsync(Alias, Id.Value);
            if (result is Result<bool, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
                return;
            }

            Notify(NotificationSeverity.Success, "Deleted", "Entry removed.");
            Navigation.NavigateTo($"/manager/content/{Alias}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task LoadTranslationsAsync()
    {
        if (!Id.HasValue)
        {
            _cultureVariants = [];
            return;
        }

        _isLoadingTranslations = true;
        try
        {
            var result = await ContentItemsApi.GetTranslationsAsync(Alias, Id.Value);
            if (result is Result<IReadOnlyList<ContentItemDetail>, AeroError>.Ok ok)
            {
                _cultureVariants = ok.Value;
                return;
            }

            if (result is Result<IReadOnlyList<ContentItemDetail>, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Translations failed to load", failure.Error.ToString());
            }
        }
        finally
        {
            _isLoadingTranslations = false;
        }
    }

    private async Task AddTranslationAsync()
    {
        if (!Id.HasValue)
        {
            return;
        }

        var missingCultures = AvailableTranslationCultures.ToList();
        if (missingCultures.Count == 0)
        {
            Notify(NotificationSeverity.Info, "Translations complete", "All supported site cultures already have entries.");
            return;
        }

        var dialogResult = await DialogService.OpenAsync<ContentAddTranslationDialog>(
            "Add Translation",
            new Dictionary<string, object?>
            {
                ["AvailableCultures"] = missingCultures,
                ["SourceSlug"] = _slug
            },
            new DialogOptions { Width = "520px", Resizable = false, Draggable = false });

        if (dialogResult is not ContentAddTranslationDialogResult decision)
        {
            return;
        }

        _isSaving = true;
        try
        {
            var request = new ForkContentItemCultureRequest(decision.Culture, decision.Slug);
            var result = await ContentItemsApi.ForkToCultureAsync(Alias, Id.Value, request);
            if (result is Result<ContentItemDetail, AeroError>.Ok ok)
            {
                Notify(NotificationSeverity.Success, "Translation created", $"{FormatCulture(ok.Value.Culture)} draft created.");
                Navigation.NavigateTo($"/manager/content/{Alias}/editor/{ok.Value.Id}?tab=translations");
                return;
            }

            if (result is Result<ContentItemDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Translation failed", failure.Error.ToString());
            }
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OpenTranslation(long id)
        => Navigation.NavigateTo($"/manager/content/{Alias}/editor/{id}?tab=translations");

    private async Task DeleteTranslationAsync(ContentItemDetail variant)
    {
        if (!Id.HasValue || variant.Id == Id.Value)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            $"Delete the {FormatCulture(variant.Culture)} translation for '{variant.Title}'?",
            "Delete Translation",
            new ConfirmOptions { OkButtonText = "Delete Translation", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        var result = await ContentItemsApi.DeleteAsync(Alias, variant.Id);
        if (result is Result<bool, AeroError>.Ok)
        {
            Notify(NotificationSeverity.Success, "Translation deleted", $"{FormatCulture(variant.Culture)} translation removed.");
            await LoadTranslationsAsync();
            return;
        }

        if (result is Result<bool, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Delete failed", failure.Error.ToString());
        }
    }

    private bool ValidateForSave()
    {
        _fieldErrors.Clear();
        if (string.IsNullOrWhiteSpace(_title))
        {
            Notify(NotificationSeverity.Warning, "Missing title", "Give this entry a title.");
            return false;
        }

        if (_allowPublicUrl && string.IsNullOrWhiteSpace(_slug))
        {
            _slug = GenerateSlug(_title);
        }

        if (!ValidateEditorValues())
        {
            Notify(NotificationSeverity.Warning, "Invalid field values", "Correct the highlighted fields before saving.");
            return false;
        }

        return true;
    }

    private bool ValidateForPublish()
    {
        if (!ValidateForSave()) return false;
        if (_typeDefinition is null) return false;

        foreach (var field in _typeDefinition.Fields.Where(field => field.Required))
        {
            if (IsFieldEmpty(field))
            {
                _fieldErrors[field.Name] = $"{FieldLabel(field)} is required before publishing.";
            }
        }

        if (_fieldErrors.Count == 0) return true;

        Notify(NotificationSeverity.Warning, "Required fields missing", "Complete the highlighted fields before publishing.");
        return false;
    }

    private bool IsFieldEmpty(ContentFieldDefinition field)
        => field.FieldType switch
        {
            "number" => !_numberValues.TryGetValue(field.Name, out var value) || value is null,
            ContentFieldTypes.Range => !_rangeValues.TryGetValue(field.Name, out var range) || range is null,
            "boolean" => false,
            "date" => !_dateValues.TryGetValue(field.Name, out var value) || value is null,
            ContentFieldTypes.List => !_listValues.TryGetValue(field.Name, out var list) || list.Count == 0,
            ContentFieldTypes.Gallery => !_galleryValues.TryGetValue(field.Name, out var gallery) || gallery.Count == 0,
            ContentFieldTypes.Dictionary => !_dictionaryValues.TryGetValue(field.Name, out var dictionary) || dictionary.All(row => string.IsNullOrWhiteSpace(row.Key)),
            _ => !_fieldValues.TryGetValue(field.Name, out var value) || string.IsNullOrWhiteSpace(value)
        };

    private Dictionary<string, JsonElement> BuildFieldsDictionary()
    {
        var dict = new Dictionary<string, JsonElement>();
        if (_typeDefinition is null) return dict;

        foreach (var field in _typeDefinition.Fields)
        {
            dict[field.Name] = field.FieldType switch
            {
                "number" => JsonSerializer.SerializeToElement(
                    _numberValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
                ContentFieldTypes.Range => JsonSerializer.SerializeToElement(
                    _rangeValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
                "boolean" => JsonSerializer.SerializeToElement(
                    _boolValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
                "date" => JsonSerializer.SerializeToElement(
                    _dateValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
                ContentFieldTypes.List => SerializeList(field),
                ContentFieldTypes.Gallery => JsonSerializer.SerializeToElement(
                    _galleryValues.GetValueOrDefault(field.Name, []),
                    ContentJsonContext.Default.Options),
                ContentFieldTypes.Dictionary => SerializeDictionary(field),
                _ => JsonSerializer.SerializeToElement(
                    _fieldValues.GetValueOrDefault(field.Name, string.Empty),
                    ContentJsonContext.Default.Options)
            };
        }

        return dict;
    }

    private void OpenMediaSelector(string fieldName)
    {
        _activeMediaField = fieldName;
        _activeGalleryField = null;
        _showMediaSelector = true;
    }

    private void OpenGallerySelector(string fieldName)
    {
        _activeGalleryField = fieldName;
        _activeMediaField = null;
        _showMediaSelector = true;
    }

    private Task CloseMediaSelector()
    {
        _showMediaSelector = false;
        _activeMediaField = null;
        _activeGalleryField = null;
        return Task.CompletedTask;
    }

    private Task ConfirmMediaSelection(List<MediaItem> selected)
    {
        if (_activeGalleryField is not null)
        {
            var maximum = _typeDefinition?.Fields.FirstOrDefault(field => field.Name == _activeGalleryField) is { } galleryField
                ? GetIntSetting(galleryField, CompositeContentFieldSettings.MaximumItems, 50)
                : 50;
            _galleryValues[_activeGalleryField] = selected
                .Select(media => media.Src)
                .Where(src => !string.IsNullOrWhiteSpace(src))
                .Distinct(StringComparer.Ordinal)
                .Take(maximum)
                .ToList();
        }
        else if (_activeMediaField is not null && selected.FirstOrDefault() is { } media)
        {
            _fieldValues[_activeMediaField] = media.Src;
        }

        _showMediaSelector = false;
        _activeMediaField = null;
        _activeGalleryField = null;
        return Task.CompletedTask;
    }

    private IReadOnlyList<string> GetAllowedValues(ContentFieldDefinition field)
    {
        if (!field.Settings.TryGetValue(CompositeContentFieldSettings.AllowedValues, out var value)
            || value.ValueKind != JsonValueKind.Array)
            return [];

        var numeric = GetStringSetting(field, CompositeContentFieldSettings.ItemType) == CompositeContentFieldSettings.Number;
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => NormalizeListValue(item, numeric))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static string? NormalizeListValue(JsonElement value, bool numeric)
    {
        if (!numeric)
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var jsonNumber))
            return jsonNumber.ToString("G29", CultureInfo.InvariantCulture);

        return value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var configuredNumber)
                ? configuredNumber.ToString("G29", CultureInfo.InvariantCulture)
                : null;
    }

    private static string FormatDictionaryValue(JsonElement value, bool numeric)
    {
        if (numeric && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number.ToString("G29", CultureInfo.InvariantCulture);

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static string? GetStringSetting(ContentFieldDefinition field, string key) =>
        field.Settings.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int GetIntSetting(ContentFieldDefinition field, string key, int fallback) =>
        field.Settings.TryGetValue(key, out var value) && value.TryGetInt32(out var parsed) ? parsed : fallback;

    private bool IsListValueSelected(string fieldName, string value) =>
        _listValues.TryGetValue(fieldName, out var selected) && selected.Contains(value, StringComparer.Ordinal);

    private void ToggleListValue(string fieldName, string value, object? checkedValue)
    {
        var selected = _listValues.GetValueOrDefault(fieldName, []);
        _listValues[fieldName] = selected;
        if (checkedValue is true && !selected.Contains(value, StringComparer.Ordinal)) selected.Add(value);
        else if (checkedValue is false) selected.RemoveAll(item => string.Equals(item, value, StringComparison.Ordinal));
    }

    private void RemoveGalleryImage(string fieldName, int index)
    {
        if (_galleryValues.TryGetValue(fieldName, out var values) && index >= 0 && index < values.Count) values.RemoveAt(index);
    }

    private void ClearColor(string fieldName) =>
        _fieldValues[fieldName] = string.Empty;

    private void MoveGalleryImage(string fieldName, int index, int direction)
    {
        if (!_galleryValues.TryGetValue(fieldName, out var values)) return;
        var target = index + direction;
        if (index < 0 || index >= values.Count || target < 0 || target >= values.Count) return;
        (values[index], values[target]) = (values[target], values[index]);
    }

    private void AddDictionaryRow(string fieldName)
    {
        if (!_dictionaryValues.TryGetValue(fieldName, out var rows))
        {
            rows = [];
            _dictionaryValues[fieldName] = rows;
        }

        rows.Add(new KeyValueEditorRow());
    }

    private void RemoveDictionaryRow(string fieldName, int index)
    {
        if (_dictionaryValues.TryGetValue(fieldName, out var rows) && index >= 0 && index < rows.Count) rows.RemoveAt(index);
    }

    private JsonElement SerializeList(ContentFieldDefinition field)
    {
        var selected = _listValues.GetValueOrDefault(field.Name, []);
        var values = GetAllowedValues(field).Where(value => selected.Contains(value, StringComparer.Ordinal)).ToList();
        if (GetStringSetting(field, CompositeContentFieldSettings.ItemType) == CompositeContentFieldSettings.Number)
        {
            var numbers = values
                .Where(value => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                .Select(value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture))
                .ToList();
            return JsonSerializer.SerializeToElement(numbers, ContentJsonContext.Default.Options);
        }
        return JsonSerializer.SerializeToElement(values, ContentJsonContext.Default.Options);
    }

    private JsonElement SerializeDictionary(ContentFieldDefinition field)
    {
        var rows = _dictionaryValues.GetValueOrDefault(field.Name, []).Where(row => !string.IsNullOrWhiteSpace(row.Key));
        if (GetStringSetting(field, CompositeContentFieldSettings.ValueType) == CompositeContentFieldSettings.Number)
        {
            var values = rows.ToDictionary(
                row => row.Key.Trim(),
                row => decimal.Parse(row.Value, NumberStyles.Number, CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.SerializeToElement(values, ContentJsonContext.Default.Options);
        }
        var textValues = rows.ToDictionary(row => row.Key.Trim(), row => row.Value, StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.SerializeToElement(textValues, ContentJsonContext.Default.Options);
    }

    private bool ValidateEditorValues()
    {
        if (_typeDefinition is null) return false;
        foreach (var field in _typeDefinition.Fields)
        {
            if (field.FieldType == ContentFieldTypes.List)
            {
                var count = _listValues.GetValueOrDefault(field.Name, []).Count;
                var minimum = GetIntSetting(field, CompositeContentFieldSettings.MinimumItems, 0);
                var maximum = GetIntSetting(field, CompositeContentFieldSettings.MaximumItems, 50);
                if (count < minimum || count > maximum) _fieldErrors[field.Name] = $"Choose between {minimum} and {maximum} options.";
            }
            else if (field.FieldType == ContentFieldTypes.Gallery)
            {
                var count = _galleryValues.GetValueOrDefault(field.Name, []).Count;
                var minimum = GetIntSetting(field, CompositeContentFieldSettings.MinimumItems, 0);
                var maximum = GetIntSetting(field, CompositeContentFieldSettings.MaximumItems, 50);
                if (count < minimum || count > maximum) _fieldErrors[field.Name] = $"Choose between {minimum} and {maximum} images.";
            }
            else if (field.FieldType == ContentFieldTypes.Dictionary)
            {
                var rows = _dictionaryValues.GetValueOrDefault(field.Name, []).Where(row => !string.IsNullOrWhiteSpace(row.Key)).ToList();
                var minimum = GetIntSetting(field, CompositeContentFieldSettings.MinimumEntries, 0);
                var maximum = GetIntSetting(field, CompositeContentFieldSettings.MaximumEntries, 50);
                if (rows.Count < minimum || rows.Count > maximum) _fieldErrors[field.Name] = $"Add between {minimum} and {maximum} values.";
                else if (rows.Any(row => !string.Equals(row.Key, row.Key.Trim(), StringComparison.Ordinal))) _fieldErrors[field.Name] = "Keys cannot start or end with spaces.";
                else if (rows.Select(row => row.Key.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count) _fieldErrors[field.Name] = "Keys must be unique.";
                else if (rows.Any(row => !DictionaryKeyPattern().IsMatch(row.Key.Trim()))) _fieldErrors[field.Name] = "Keys must be 1-64 characters and use letters, numbers, internal spaces, dots, dashes, or underscores.";
                else if (GetStringSetting(field, CompositeContentFieldSettings.ValueType) == CompositeContentFieldSettings.Number
                    && rows.Any(row => !decimal.TryParse(row.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
                    _fieldErrors[field.Name] = "Every value must be a number.";
                else if (GetStringSetting(field, CompositeContentFieldSettings.ValueType) != CompositeContentFieldSettings.Number
                    && rows.Any(row => row.Value.Length > 1024))
                    _fieldErrors[field.Name] = "Text values cannot exceed 1,024 characters.";
            }
            else if (field.FieldType == ContentFieldTypes.Range
                     && _rangeValues.GetValueOrDefault(field.Name) is { } range)
            {
                var start = GetIntSetting(
                    field,
                    RangeContentFieldSettings.Start,
                    0);
                var end = GetIntSetting(
                    field,
                    RangeContentFieldSettings.End,
                    10);
                if (range < start || range > end)
                {
                    _fieldErrors[field.Name] =
                        $"Choose a whole number from {start} through {end}.";
                }
                else if (!GetBoolSetting(
                             field,
                             RangeContentFieldSettings.AllowNegative)
                         && range < 0)
                {
                    _fieldErrors[field.Name] =
                        "Negative values are not allowed.";
                }
            }
        }
        return _fieldErrors.Count == 0;
    }

    private void Cancel()
        => Navigation.NavigateTo($"/manager/content/{Alias}");

    private void OpenPublishedPagePreview()
        => _showPublishedPagePreview = true;

    private void ClosePublishedPagePreview()
        => _showPublishedPagePreview = false;

    private void SwitchTab(string tab)
        => _activeTab = tab;

    private static string FieldLabel(ContentFieldDefinition field)
        => string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label!;

    private int? ActiveGalleryMaximumSelections =>
        _activeGalleryField is not null
        && _typeDefinition?.Fields.FirstOrDefault(candidate => candidate.Name == _activeGalleryField) is { } definitionField
            ? GetIntSetting(definitionField, CompositeContentFieldSettings.MaximumItems, 50)
            : null;

    private sealed class KeyValueEditorRow(string key = "", string value = "")
    {
        public string Key { get; set; } = key;
        public string Value { get; set; } = value;
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9 _.-]{0,62}[A-Za-z0-9_.-])?$", RegexOptions.CultureInvariant)]
    private static partial Regex DictionaryKeyPattern();

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("MMM d, yyyy") ?? "-";

    private static string FormatCulture(string? culture)
    {
        var normalized = NormalizeCulture(culture);
        return string.IsNullOrWhiteSpace(normalized) ? "Default" : normalized;
    }

    private static string NormalizeCulture(string? culture)
        => culture?.Trim() ?? string.Empty;

    private static decimal? TryParseDecimal(string? value)
        => decimal.TryParse(value, out var parsed) ? parsed : null;

    private static string GenerateSlug(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9\s_-]", "");
        slug = Regex.Replace(slug, @"[\s_]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        return slug;
    }

    private void Notify(NotificationSeverity severity, string summary, string detail)
        => NotificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = 4000
        });

    private sealed record ContentParentOption(
        long Id,
        string Title,
        string ContentTypeAlias,
        string Breadcrumb,
        bool CanAcceptChildren);
}
