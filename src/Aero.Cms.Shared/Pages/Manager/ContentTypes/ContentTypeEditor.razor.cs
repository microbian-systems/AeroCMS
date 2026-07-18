using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
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
        new("number", L["Number"], "pin", L["Prices, counts, rankings, or measurements."]),
        new("boolean", L["Yes/No"], "toggle_on", L["A simple on/off choice."]),
        new("url", L["Link"], "link", L["Website or call-to-action URL."]),
        new("date", L["Date"], "event", L["Dates and milestones."]),
        new("reference", L["Reference"], "account_tree", L["Link to another content entry."])
    ];

    private bool IsNew => string.IsNullOrWhiteSpace(Alias);
    private EditorTab _activeTab = EditorTab.Basics;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _aliasLocked;
    private bool _useCustomTemplate;
    private int? _selectedFieldIndex;
    private RadzenDataGrid<ContentItemSummary>? _entriesGrid;
    private IEnumerable<ContentItemSummary> _entries = [];
    private int _entriesCount;
    private bool _entriesLoading;
    private string _entriesSearchText = string.Empty;

    private string Name { get; set; } = string.Empty;
    private string AliasValue { get; set; } = string.Empty;
    private string? Description { get; set; }
    private string? Category { get; set; }
    private bool AllowPublicUrl { get; set; }
    private bool HideFromSearch { get; set; }
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
            HideFromSearch = detail.HideFromSearch;
            ScribanTemplate = detail.ScribanTemplate;
            _useCustomTemplate = !string.IsNullOrWhiteSpace(detail.ScribanTemplate);
            Fields = detail.Fields.Select(CloneField).ToList();
            _aliasLocked = true;
        }
        else if (result is Result<ContentTypeDetail, AeroError>.Failure failure)
        {
            Notify(NotificationSeverity.Error, "Load failed", failure.Error.ToString());
            Navigation.NavigateTo("/manager/content-types");
        }

        _isLoading = false;
    }

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

        Fields.Add(new ContentFieldDefinition
        {
            Name = handle,
            FieldType = fieldType,
            Label = baseLabel,
            Placeholder = option.Description
        });

        _selectedFieldIndex = Fields.Count - 1;
    }

    private void SelectField(int index)
    {
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

    private void AutoGenerateTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<article class="content-type-{AliasValue}">""");
        foreach (var field in Fields)
        {
            sb.AppendLine($"""  <section class="aero-field aero-field-{field.FieldType}">""");
            sb.AppendLine($"    {{{{ block.{field.Name} }}}}");
            sb.AppendLine("  </section>");
        }
        sb.AppendLine("</article>");
        ScribanTemplate = sb.ToString();
    }

    private async Task SaveAsync()
    {
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
                HideFromSearch,
                Fields,
                _useCustomTemplate ? ScribanTemplate : null,
                null);

            var result = IsNew
                ? await ContentTypesApi.CreateAsync(request)
                : await ContentTypesApi.UpdateAsync(Alias!, request);

            if (result is Result<ContentTypeDetail, AeroError>.Failure failure)
            {
                Notify(NotificationSeverity.Error, "Save failed", failure.Error.ToString());
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

        var duplicate = Fields
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            Notify(NotificationSeverity.Warning, "Duplicate field handle", $"'{duplicate.Key}' is used more than once.");
            _activeTab = EditorTab.Fields;
            return false;
        }

        return true;
    }

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
