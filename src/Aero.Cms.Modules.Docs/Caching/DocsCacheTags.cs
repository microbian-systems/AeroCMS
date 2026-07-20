namespace Aero.Cms.Modules.Docs.Caching;

/// <summary>
/// Produces the output-cache tags associated with documentation index and page responses.
/// </summary>
public static class DocsCacheTags
{
    /// <summary>
    /// Gets the tag shared by documentation index responses.
    /// </summary>
    public const string DocsIndex = "docs-index";

    /// <summary>
    /// Produces the tag for a page identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <returns>A tag in the form <c>doc-id-{id}</c>.</returns>
    public static string DocById(long id) => $"doc-id-{id}";

    /// <summary>
    /// Produces the case-insensitive tag for a page slug.
    /// </summary>
    /// <param name="slug">The non-<see langword="null"/> slug to normalize.</param>
    /// <returns>A tag whose slug component is converted with invariant casing.</returns>
    /// <exception cref="NullReferenceException"><paramref name="slug"/> is <see langword="null"/>.</exception>
    public static string DocBySlug(string slug) => $"doc-slug-{slug.ToLowerInvariant()}";
}
