using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Creates a draft culture variant from an existing post without persisting it.
/// </summary>
public static class PostCultureForker
{
    /// <summary>
    /// Copies translatable content and editorial relationships into a new draft post.
    /// </summary>
    /// <param name="source">The source post whose site and content are copied.</param>
    /// <param name="targetPostId">The identifier assigned to the returned variant.</param>
    /// <param name="targetCulture">The target culture to normalize.</param>
    /// <param name="targetSlug">The target slug; leading and trailing separators are removed.</param>
    /// <returns>
    /// An unpersisted draft linked to the source translation group. Audit timestamps and
    /// publication timestamps are not copied.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
public static PostDocument Fork(PostDocument source, long targetPostId, string targetCulture, string targetSlug)
    {
        ArgumentNullException.ThrowIfNull(source);

        var translationGroupId = source.TranslationGroupId ?? source.Id;

        return new PostDocument
        {
            Id = targetPostId,
            SiteId = source.SiteId,
            TranslationGroupId = translationGroupId,
            SourcePostId = source.Id,
            SeriesId = source.SeriesId,
            Culture = ContentSlugDocument.NormalizeCulture(targetCulture),
            Slug = targetSlug.Trim().Trim('/'),
            Title = source.Title,
            Excerpt = source.Excerpt,
            SeoTitle = source.SeoTitle,
            SeoDescription = source.SeoDescription,
            IncludeInSearch = source.IncludeInSearch,
            IncludeInPublicAi = source.IncludeInPublicAi,
            PublicationState = ContentPublicationState.Draft,
            MarkdownContent = source.MarkdownContent,
            TagIds = source.TagIds.ToList(),
            CategoryIds = source.CategoryIds.ToList(),
            AuthorId = source.AuthorId,
            ImageUrl = source.ImageUrl,
            Likes = source.Likes
        };
    }

}
