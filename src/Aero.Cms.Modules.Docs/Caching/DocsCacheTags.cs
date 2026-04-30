namespace Aero.Cms.Modules.Docs.Caching;

/// <summary>
/// Provides cache tag constants for Docs module output caching.
/// </summary>
public static class DocsCacheTags
{
    /// <summary>
    /// Tag for the docs index page.
    /// </summary>
    public const string DocsIndex = "docs-index";

    /// <summary>
    /// Generates a cache tag for a specific doc page by its ID.
    /// </summary>
    public static string DocById(long id) => $"doc-id-{id}";

    /// <summary>
    /// Generates a cache tag for a specific doc page by its slug.
    /// </summary>
    public static string DocBySlug(string slug) => $"doc-slug-{slug.ToLowerInvariant()}";
}
