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
    public const string Range = "range";
    public const string Color = "color";
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

/// <summary>Setting names and stable choices for content-reference fields.</summary>
public static class ReferenceContentFieldSettings
{
    public const string TargetContentType = "targetContentType";
    public const string AllowMultiple = "allowMultiple";
    public const string SelectionMode = "selectionMode";
    public const string SelectionModeHierarchy = "hierarchy";
    public const string SelectLeafOnly = "selectLeafOnly";
    public const string ShowAncestors = "showAncestors";
    public const string DependsOnField = "dependsOnField";
    public const string TargetFilterField = "targetFilterField";
}

/// <summary>Setting names for an inclusive integer range field.</summary>
public static class RangeContentFieldSettings
{
    public const string Start = "start";
    public const string End = "end";
    public const string AllowNegative = "allowNegative";
}
