using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Aero.Cms.Shared.Pages.Manager.ContentTypes;

public partial class ContentTypeEditor
{
    [Parameter] public string? Alias { get; set; }

    [Inject] private IContentTypesHttpClient ContentTypesApi { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private readonly List<DropDownItem> RenderModes =
    [
        new("Standard field display", "DynamicBlock"),
        new("Block layout", "BlockLayout")
    ];

    private readonly List<FieldTypeOption> FieldOptions =
    [
        new("text", "Short text", "title", "Single line names, headlines, and labels."),
        new("richtext", "Rich text", "notes", "Longer formatted copy."),
        new("image", "Image", "image", "Photo or graphic URL."),
        new("number", "Number", "pin", "Prices, counts, rankings, or measurements."),
        new("boolean", "Yes/No", "toggle_on", "A simple on/off choice."),
        new("url", "Link", "link", "Website or call-to-action URL."),
        new("date", "Date", "event", "Dates and milestones."),
        new("reference", "Reference", "account_tree", "Link to another content entry.")
    ];

    private bool IsNew => string.IsNullOrWhiteSpace(Alias);
    private EditorTab _activeTab = EditorTab.Basics;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _aliasLocked;
    private bool _useCustomTemplate;
    private int? _selectedFieldIndex;

    private string Name { get; set; } = string.Empty;
    private string AliasValue { get; set; } = string.Empty;
    private string? Description { get; set; }
    private string? Category { get; set; }
    private bool AllowPublicUrl { get; set; }
    private string RenderMode { get; set; } = "DynamicBlock";
    private string? ScribanTemplate { get; set; }
    private List<ContentFieldDefinition> Fields { get; set; } = [];

    private string DisplayTypeName => string.IsNullOrWhiteSpace(Name) ? "content" : Name.ToLowerInvariant();

    private ContentFieldDefinition? SelectedField =>
        _selectedFieldIndex is int index && index >= 0 && index < Fields.Count
            ? Fields[index]
            : null;

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
            RenderMode = detail.RenderMode;
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
        => TryGetSetting(field, key, out var value) switch
        {
            true when value is int i => i,
            true when value is JsonElement element && element.TryGetInt32(out var i) => i,
            true when int.TryParse(value?.ToString(), out var i) => i,
            _ => null
        };

    private decimal? GetDecimalSetting(ContentFieldDefinition field, string key)
        => TryGetSetting(field, key, out var value) switch
        {
            true when value is decimal d => d,
            true when value is JsonElement element && element.TryGetDecimal(out var d) => d,
            true when decimal.TryParse(value?.ToString(), out var d) => d,
            _ => null
        };

    private string? GetStringSetting(ContentFieldDefinition field, string key)
    {
        if (!TryGetSetting(field, key, out var value)) return null;
        return value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText()
            : value?.ToString();
    }

    private static bool TryGetSetting(ContentFieldDefinition field, string key, out object? value)
        => field.Settings.TryGetValue(key, out value);

    private static void SetSetting(ContentFieldDefinition field, string key, object? value)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            field.Settings.Remove(key);
            return;
        }

        field.Settings[key] = value;
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
                Fields,
                _useCustomTemplate ? ScribanTemplate : null,
                RenderMode,
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
            Settings = field.Settings.ToDictionary(pair => pair.Key, pair => pair.Value)
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
        Display
    }

    private sealed record DropDownItem(string Text, string Value);

    private sealed record FieldTypeOption(string Value, string Label, string Icon, string Description);
}
