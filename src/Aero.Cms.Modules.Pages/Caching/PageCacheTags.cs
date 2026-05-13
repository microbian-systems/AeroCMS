using Aero.Cms.Abstractions.Enums;

namespace Aero.Cms.Modules.Pages.Caching;

/// <summary>
/// Provides cache tag constants for Pages module output caching.
/// Tags flow through two layers:
/// 1. <b>OutputCache</b> — set at write-time via <c>CmsOutputCachePolicy.ServeResponseAsync</c>,
///    evicted via <c>IOutputCacheStore.EvictByTagAsync</c>.
/// 2. <b>FusionCache</b> — set @ write-time via <c>IFusionCache.SetAsync(key, value, tags)</c>,
///    evicted via <c>IFusionCache.RemoveByTagAsync</c>.
///
/// Coarse tag (<c>PagesList</c>) evicts all pages. Fine-grained tags
/// (<c>PageById</c>, <c>PageBySlug</c>) allow single-page eviction without
/// invalidating the entire page cache.
/// </summary>
public static class PageCacheTags
{
    /// <summary>
    /// Tag for the list of all published pages.
    /// </summary>
    public const string PagesList = "pages-list";

    /// <summary>
    /// Generates a cache tag for a specific page by its ID.
    /// Format: <c>page-id-{id}</c>
    /// </summary>
    public static string PageById(long id) => $"page-id-{id}";

    /// <summary>
    /// Generates a cache tag for a specific page by its slug.
    /// Format: <c>page-slug-{slug}</c>
    /// </summary>
    public static string PageBySlug(string slug) => $"page-slug-{slug.ToLowerInvariant()}";

    /// <summary>
    /// Generates a cache tag for pages by kind (homepage, blog listing, etc.).
    /// Format: <c>page-kind-{kind}</c>
    /// </summary>
    public static string PageByKind(PageKind kind) => $"page-kind-{kind}";

    /// <summary>
    /// Generates a cache key used by FusionCache for per-page-by-ID lookups.
    /// Format: <c>cms:page:{siteId}:id:{pageId}</c>
    /// </summary>
    public static string FusionKeyById(long siteId, long pageId) => $"cms:page:{siteId}:id:{pageId}";
}
