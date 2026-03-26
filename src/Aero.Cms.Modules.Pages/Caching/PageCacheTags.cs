namespace Aero.Cms.Modules.Pages.Caching;

/// <summary>
/// Provides cache tag constants for Pages module output caching.
/// </summary>
public static class PageCacheTags
{
    /// <summary>
    /// Tag for the list of all published pages.
    /// </summary>
    public const string PagesList = "pages-list";

    /// <summary>
    /// Generates a cache tag for a specific page by its ID.
    /// </summary>
    public static string PageById(long id) => $"page-id-{id}";

    /// <summary>
    /// Generates a cache tag for a specific page by its slug.
    /// </summary>
    public static string PageBySlug(string slug) => $"page-slug-{slug.ToLowerInvariant()}";

    /// <summary>
    /// Generates a cache tag for pages by kind (homepage, blog listing, etc.).
    /// </summary>
    public static string PageByKind(PageKind kind) => $"page-kind-{kind}";
}
