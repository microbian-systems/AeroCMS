namespace Aero.Cms.Abstractions.Content;

/// <summary>Stable aliases used by built-in content fields.</summary>
public static class ContentFieldTypes
{
    public const string Text = "text";
    public const string RichText = "richtext";
    public const string Image = "image";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string Url = "url";
    public const string Date = "date";
    public const string Reference = "reference";
    public const string List = "list";
    public const string Gallery = "gallery";
    public const string Dictionary = "dictionary";
}

/// <summary>Setting names and scalar choices for bounded composite fields.</summary>
public static class CompositeContentFieldSettings
{
    public const string ItemType = "itemType";
    public const string ValueType = "valueType";
    public const string AllowedValues = "allowedValues";
    public const string MinimumItems = "minItems";
    public const string MaximumItems = "maxItems";
    public const string MinimumEntries = "minEntries";
    public const string MaximumEntries = "maxEntries";
    public const string Text = "text";
    public const string Number = "number";
}
