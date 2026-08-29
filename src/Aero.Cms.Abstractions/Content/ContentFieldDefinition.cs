using System.Text.Json;
using Aero.Cms.Abstractions.Ai.Knowledge;
using Aero.Cms.Abstractions.Content.Localization;

namespace Aero.Cms.Abstractions.Content;

/// <summary>
/// Represents a class for ContentFieldDefinition.
/// </summary>
public sealed class ContentFieldDefinition
{
    private Dictionary<string, JsonElement> settings = [];

    /// <summary>Field name used as the key in ContentItem.Fields</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field type alias, including bounded aliases such as "range", "color", "list",
    /// "gallery", and "dictionary".
    /// </summary>
    public string FieldType { get; set; } = "text";

    /// <summary>Display label for the admin UI</summary>
    public string? Label { get; set; }

        /// <summary>
    /// Gets or sets the Required.
    /// </summary>
public bool Required { get; set; }

    /// <summary>Default value when the field is empty</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Placeholder text for the admin UI editor</summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets or sets how this field participates in a culture fork. New fields copy their
    /// source value into a fork and remain independent until an editor explicitly changes them.
    /// </summary>
    public ContentFieldLocalizationMode LocalizationMode { get; set; } = ContentFieldLocalizationMode.CopyOnFork;

    /// <summary>
    /// Gets or sets whether the field participates in exact-value filtering and sorting.
    /// Reference fields are always indexed by the content service.
    /// </summary>
    public bool Indexed { get; set; }

    /// <summary>
    /// Gets or sets whether the field contributes text to the full-text search document.
    /// </summary>
    public bool FullTextSearchable { get; set; }

    /// <summary>
    /// Gets or sets whether the field contributes text to the semantic embedding document.
    /// </summary>
    public bool SemanticSearchable { get; set; }

    /// <summary>
    /// Gets or sets the AI retrieval exposure for this field. New fields fail closed as internal-only.
    /// This setting does not override record-level search or public-AI inclusion.
    /// </summary>
    public AeroAiFieldExposure AiExposure { get; set; } = AeroAiFieldExposure.Internal;

    /// <summary>Validation rules, editor hints, etc. consumed by FluentValidation + admin UI</summary>
    public Dictionary<string, JsonElement> Settings
    {
        get => settings;
        set => settings = value ?? [];
    }
}
