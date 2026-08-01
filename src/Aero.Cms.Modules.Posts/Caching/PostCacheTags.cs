namespace Aero.Cms.Modules.Posts.Caching;

/// <summary>
/// Builds stable cache tags used to invalidate post-related cache entries.
/// </summary>
public static class PostCacheTags
{
    /// <summary>
    /// Gets the tag shared by cached blog index entries.
    /// </summary>
    public const string BlogIndex = "blog-index";

    /// <summary>
    /// Builds the cache tag for a post identifier.
    /// </summary>
    /// <param name="id">The persisted post identifier.</param>
    /// <returns>The identifier-specific cache tag.</returns>
    public static string PostPostById(long id) => $"blog-post-id-{id}";

    /// <summary>
    /// Builds the cache tag for a post slug.
    /// </summary>
    /// <param name="slug">The slug to normalize to invariant lowercase.</param>
    /// <returns>The slug-specific cache tag.</returns>
    public static string PostBySlug(string slug) => $"blog-post-slug-{slug.ToLowerInvariant()}";

    /// <summary>
    /// Builds the cache tag for posts assigned to a tag.
    /// </summary>
    /// <param name="tagId">The tag identifier.</param>
    /// <returns>The tag-filtered cache tag.</returns>
    public static string PostsByTag(long tagId) => $"blog-posts-tag-{tagId}";

    /// <summary>
    /// Builds the cache tag for posts assigned to a category.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <returns>The category-filtered cache tag.</returns>
    public static string PostsByCategory(long categoryId) => $"blog-posts-category-{categoryId}";

    /// <summary>
    /// Builds the cache tag for posts assigned to an author.
    /// </summary>
    /// <param name="authorId">The author identifier.</param>
    /// <returns>The author-filtered cache tag.</returns>
    public static string PostsByAuthor(long authorId) => $"blog-posts-author-{authorId}";

    /// <summary>
    /// Gets the tag shared by cached tag lists.
    /// </summary>
    public const string TagsList = "blog-tags-list";

    /// <summary>
    /// Gets the tag shared by cached category lists.
    /// </summary>
    public const string CategoriesList = "blog-categories-list";
}
