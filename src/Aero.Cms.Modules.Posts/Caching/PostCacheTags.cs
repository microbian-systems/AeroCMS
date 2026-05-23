namespace Aero.Cms.Modules.Posts.Caching;

/// <summary>
/// Provides cache tag constants for Blog module output caching.
/// </summary>
public static class PostCacheTags
{
    /// <summary>
    /// Tag for the blog index page (list of all posts).
    /// </summary>
    public const string BlogIndex = "blog-index";

    /// <summary>
    /// Generates a cache tag for a specific blog post by its ID.
    /// </summary>
    public static string PostPostById(long id) => $"blog-post-id-{id}";

    /// <summary>
    /// Generates a cache tag for a specific blog post by its slug.
    /// </summary>
    public static string PostBySlug(string slug) => $"blog-post-slug-{slug.ToLowerInvariant()}";

    /// <summary>
    /// Generates a cache tag for blog posts filtered by tag.
    /// </summary>
    public static string PostsByTag(long tagId) => $"blog-posts-tag-{tagId}";

    /// <summary>
    /// Generates a cache tag for blog posts filtered by category.
    /// </summary>
    public static string PostsByCategory(long categoryId) => $"blog-posts-category-{categoryId}";

    /// <summary>
    /// Generates a cache tag for blog posts by a specific author.
    /// </summary>
    public static string PostsByAuthor(long authorId) => $"blog-posts-author-{authorId}";

    /// <summary>
    /// Tag for the list of all blog tags.
    /// </summary>
    public const string TagsList = "blog-tags-list";

    /// <summary>
    /// Tag for the list of all blog categories.
    /// </summary>
    public const string CategoriesList = "blog-categories-list";
}
