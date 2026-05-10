namespace Aero.Cms.Abstractions.Content;

public sealed class ContentFieldDefinition
{
    /// <summary>Field name used as the key in ContentItem.Fields</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Field type alias: "text", "richtext", "image", "url", "number", "date", "boolean", "media"</summary>
    public string FieldType { get; set; } = "text";

    /// <summary>Display label for the admin UI</summary>
    public string? Label { get; set; }

    public bool Required { get; set; }

    /// <summary>Default value when the field is empty</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Placeholder text for the admin UI editor</summary>
    public string? Placeholder { get; set; }

    /// <summary>Validation rules, editor hints, etc. consumed by FluentValidation + admin UI</summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}
