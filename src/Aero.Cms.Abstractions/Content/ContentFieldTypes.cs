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
    public const string TargetKind = "targetKind";
    public const string TargetKindContentType = "contentType";
    public const string TargetKindCmsDocument = "cmsDocument";
    /// <summary>A provider-qualified, query-backed or otherwise virtual content entry.</summary>
    public const string TargetKindContentEntry = "contentEntry";
    public const string TargetContentTypeId = "targetContentTypeId";
    public const string AllowedSources = "allowedSources";
    /// <summary>Optional exact provider keys. An omitted value admits every server-registered provider.</summary>
    public const string AllowedProviders = "allowedProviders";
    public const string AllowMultiple = "allowMultiple";
    public const string SelectionMode = "selectionMode";
    public const string SelectionModeHierarchy = "hierarchy";
    public const string SelectLeafOnly = "selectLeafOnly";
    public const string ShowAncestors = "showAncestors";
    public const string DependsOnField = "dependsOnField";
    public const string TargetFilterField = "targetFilterField";
    /// <summary>
    /// Ordered field names to display when previewing a query-backed content entry.
    /// </summary>
    public const string PreviewFields = "previewFields";
}

/// <summary>Stable source keys for first-class CMS document references.</summary>
public static class CmsContentReferenceSources
{
    public const string Pages = "pages";
    public const string Posts = "posts";
    public const string Docs = "docs";
    public const string ContentItemPages = "content:*";
    public const string ContentItemPagePrefix = "content:";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(
            [Pages, Posts, Docs, ContentItemPages],
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the stable source key used for public entries of one dynamic
    /// content type.
    /// </summary>
    public static string ForContentType(string alias) =>
        $"{ContentItemPagePrefix}{alias.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Reads the content-type alias from a dynamic public-content source key.
    /// </summary>
    public static bool TryGetContentTypeAlias(
        string? source,
        out string alias)
    {
        alias = string.Empty;
        if (string.IsNullOrWhiteSpace(source)
            || !source.StartsWith(
                ContentItemPagePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        alias = source[ContentItemPagePrefix.Length..].Trim();
        return alias.Length > 0 && alias != "*";
    }

    /// <summary>
    /// Returns whether a concrete persisted source is supported by Aero.
    /// </summary>
    public static bool IsSupportedSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && (string.Equals(source, Pages, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, Posts, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, Docs, StringComparison.OrdinalIgnoreCase)
            || TryGetContentTypeAlias(source, out _));

    /// <summary>
    /// Returns whether a concrete source is admitted by the field's exact
    /// sources or by the public-content wildcard.
    /// </summary>
    public static bool IsAllowedSource(
        string? source,
        IEnumerable<string> allowedSources)
    {
        if (!IsSupportedSource(source))
        {
            return false;
        }

        var allowed = allowedSources.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        return allowed.Contains(source!)
            || (TryGetContentTypeAlias(source, out _)
                && allowed.Contains(ContentItemPages));
    }
}

/// <summary>Setting names for an inclusive integer range field.</summary>
public static class RangeContentFieldSettings
{
    public const string Start = "start";
    public const string End = "end";
    public const string AllowNegative = "allowNegative";
}
