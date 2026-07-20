namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Centralizes normalized FusionCache keys and shared Fusion/Output Cache tags
/// for runtime-defined content.
/// </summary>
internal static class ContentCacheKeys
{
    /// <summary>Builds a site-qualified content-type identifier key.</summary>
    public static string TypeById(long siteId, long typeId) =>
        $"cms:content-type:{siteId}:id:{typeId}";

    /// <summary>Builds a site-qualified normalized content-type alias key.</summary>
    public static string TypeByAlias(long siteId, string alias) =>
        $"cms:content-type:{siteId}:alias:{Normalize(alias)}";

    /// <summary>Builds the key for a site's cached content-type list.</summary>
    public static string TypeList(long siteId) =>
        $"cms:content-type:{siteId}:list";

    /// <summary>Builds a globally keyed content-item identifier key.</summary>
    public static string ItemById(long itemId) =>
        $"cms:content-item:id:{itemId}";

    /// <summary>Builds a site-qualified normalized, but type-agnostic, item-slug key.</summary>
    public static string ItemBySlug(long siteId, string slug) =>
        $"cms:content-item:{siteId}:slug:{Normalize(slug)}";

    /// <summary>Builds a site-, type-, culture-, and slug-qualified item key.</summary>
    public static string ItemByTypedSlug(
        long siteId,
        string typeAlias,
        string culture,
        string slug) =>
        $"cms:content-item:{siteId}:{Normalize(typeAlias)}:{Normalize(culture)}:slug:{Normalize(slug)}";

    /// <summary>Builds the tag shared by every content-type cache entry for a site.</summary>
    public static string ContentTypesTag(long siteId) =>
        $"content-types:{siteId}";

    /// <summary>Builds the tag for one content-type alias within a site.</summary>
    public static string ContentTypeTag(long siteId, string alias) =>
        $"content-type:{siteId}:{Normalize(alias)}";

    /// <summary>Builds the tag shared by content-item cache entries for a site.</summary>
    public static string ContentItemsTag(long siteId) =>
        $"content-items:{siteId}";

    /// <summary>Builds the tag shared by a site's items of one content type.</summary>
    public static string ContentItemsByTypeTag(long siteId, string alias) =>
        $"content-items:{siteId}:{Normalize(alias)}";

    /// <summary>Builds the tag for one content item within a site.</summary>
    public static string ContentItemTag(long siteId, long itemId) =>
        $"content-item:{siteId}:{itemId}";

    /// <summary>Builds the tag for one site's normalized type, culture, and slug identity.</summary>
    public static string ContentItemSlugTag(
        long siteId,
        string typeAlias,
        string culture,
        string slug) =>
        $"content-item-slug:{siteId}:{Normalize(typeAlias)}:{Normalize(culture)}:{Normalize(slug)}";

    /// <summary>Builds the output-cache tag shared by all public content for a site.</summary>
    public static string ContentPublicTag(long siteId) =>
        $"content-public:{siteId}";

    /// <summary>
    /// Trims whitespace and surrounding slashes and lowercases a key component.
    /// </summary>
    /// <returns>An underscore for null, blank, or slash-only values.</returns>
    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "_"
            : value.Trim().Trim('/').ToLowerInvariant();
}
