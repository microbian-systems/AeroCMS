using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts;

/// <summary>
/// Represents a class for PostCultureForker.
/// </summary>
public static class PostCultureForker
{
        /// <summary>
    /// Fork method.
    /// </summary>
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
