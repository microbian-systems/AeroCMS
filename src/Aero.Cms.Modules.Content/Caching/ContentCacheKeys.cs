namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Centralizes normalized FusionCache keys and shared Fusion/Output Cache tags
/// for runtime-defined content.
/// </summary>
internal static class ContentCacheKeys
{
    public static string TypeById(long siteId, long typeId) =>
        $"cms:content-type:{siteId}:id:{typeId}";

    public static string TypeByAlias(long siteId, string alias) =>
        $"cms:content-type:{siteId}:alias:{Normalize(alias)}";

    public static string TypeList(long siteId) =>
        $"cms:content-type:{siteId}:list";

    public static string ItemById(long itemId) =>
        $"cms:content-item:id:{itemId}";

    public static string ItemBySlug(long siteId, string slug) =>
        $"cms:content-item:{siteId}:slug:{Normalize(slug)}";

    public static string ItemByTypedSlug(
        long siteId,
        string typeAlias,
        string culture,
        string slug) =>
        $"cms:content-item:{siteId}:{Normalize(typeAlias)}:{Normalize(culture)}:slug:{Normalize(slug)}";

    public static string ContentTypesTag(long siteId) =>
        $"content-types:{siteId}";

    public static string ContentTypeTag(long siteId, string alias) =>
        $"content-type:{siteId}:{Normalize(alias)}";

    public static string ContentItemsTag(long siteId) =>
        $"content-items:{siteId}";

    public static string ContentItemsByTypeTag(long siteId, string alias) =>
        $"content-items:{siteId}:{Normalize(alias)}";

    public static string ContentItemTag(long siteId, long itemId) =>
        $"content-item:{siteId}:{itemId}";

    public static string ContentItemSlugTag(
        long siteId,
        string typeAlias,
        string culture,
        string slug) =>
        $"content-item-slug:{siteId}:{Normalize(typeAlias)}:{Normalize(culture)}:{Normalize(slug)}";

    public static string ContentPublicTag(long siteId) =>
        $"content-public:{siteId}";

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "_"
            : value.Trim().Trim('/').ToLowerInvariant();
}
