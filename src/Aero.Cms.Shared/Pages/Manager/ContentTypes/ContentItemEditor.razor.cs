using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly Dictionary<string, bool> _boolValues = [];
    private readonly Dictionary<string, DateTime?> _dateValues = [];
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
                case "boolean":
                    _boolValues.TryAdd(field.Name, bool.TryParse(field.DefaultValue, out var value) && value);
                    break;
                case "date":
                    _dateValues.TryAdd(field.Name, DateTime.TryParse(field.DefaultValue, out var date) ? date : null);
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
                case "boolean":
                    _boolValues[field.Name] = element.ValueKind == JsonValueKind.True;
                    break;
                case "date":
                    _dateValues[field.Name] = element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetDateTime();
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
            "boolean" => false,
            "date" => !_dateValues.TryGetValue(field.Name, out var value) || value is null,
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
                "boolean" => JsonSerializer.SerializeToElement(
                    _boolValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
                "date" => JsonSerializer.SerializeToElement(
                    _dateValues.GetValueOrDefault(field.Name),
                    ContentJsonContext.Default.Options),
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
        _showMediaSelector = true;
    }

    private Task CloseMediaSelector()
    {
        _showMediaSelector = false;
        _activeMediaField = null;
        return Task.CompletedTask;
    }

    private Task ConfirmMediaSelection(List<MediaItem> selected)
    {
        if (_activeMediaField is not null && selected.FirstOrDefault() is { } media)
        {
            _fieldValues[_activeMediaField] = media.Src;
        }

        _showMediaSelector = false;
        _activeMediaField = null;
        return Task.CompletedTask;
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
