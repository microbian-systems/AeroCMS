using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Entities;

namespace Aero.Cms.Modules.Posts;

public static class PostCultureForker
{
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
            Content = CloneContent(source.Content),
            TagIds = source.TagIds.ToList(),
            CategoryIds = source.CategoryIds.ToList(),
            AuthorId = source.AuthorId,
            ImageUrl = source.ImageUrl,
            Likes = source.Likes
        };
    }

    private static List<BlockBase> CloneContent(IEnumerable<BlockBase>? content)
    {
        if (content is null)
            return [];

        return content.Select(CloneBlock).ToList();
    }

    private static BlockBase CloneBlock(BlockBase block)
    {
        return block switch
        {
            MarkdownBlock markdown => new MarkdownBlock
            {
                Id = Snowflake.NewId(),
                Content = markdown.Content,
                Order = markdown.Order
            },
            _ => block
        };
    }
}
