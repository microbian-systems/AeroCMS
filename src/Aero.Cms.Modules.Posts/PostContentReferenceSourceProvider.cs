using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Posts;

/// <summary>Exposes current-site posts to dynamic CMS reference fields.</summary>
public sealed class PostContentReferenceSourceProvider(IQuerySession session)
    : IContentReferenceSourceProvider
{
    public string SourceKey => CmsContentReferenceSources.Posts;
    public string DisplayName => "Posts";

    public async Task<Result<IReadOnlyList<CmsContentReferenceOption>>> SearchAsync(
        long siteId,
        string? culture,
        string? search,
        int take,
        CancellationToken ct = default)
    {
        try
        {
            var requestedCulture = culture?.Trim();
            var term = search?.Trim().ToLowerInvariant();
            IQueryable<PostDocument> query;
            if (requestedCulture is { Length: > 0 }
                && term is { Length: > 0 })
            {
                query = session.Query<PostDocument>().Where(post =>
                    post.SiteId == siteId
                    && post.Culture == requestedCulture
                    && (post.Title.ToLower().Contains(term)
                        || post.Slug.ToLower().Contains(term)
                        || (post.Excerpt != null
                            && post.Excerpt.ToLower().Contains(term))));
            }
            else if (requestedCulture is { Length: > 0 })
            {
                query = session.Query<PostDocument>().Where(post =>
                    post.SiteId == siteId
                    && post.Culture == requestedCulture);
            }
            else if (term is { Length: > 0 })
            {
                query = session.Query<PostDocument>().Where(post =>
                    post.SiteId == siteId
                    && (post.Title.ToLower().Contains(term)
                        || post.Slug.ToLower().Contains(term)
                        || (post.Excerpt != null
                            && post.Excerpt.ToLower().Contains(term))));
            }
            else
            {
                query = session.Query<PostDocument>().Where(post =>
                    post.SiteId == siteId);
            }

            var posts = await ((ISableQueryable<PostDocument>)query)
                .OrderBy(post => post.Title)
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            var options = posts
                .Select(post => new CmsContentReferenceOption(
                    post.Id.ToString(CultureInfo.InvariantCulture),
                    post.Title,
                    post.Slug,
                    post.Culture))
                .ToArray();
            return new Result<IReadOnlyList<CmsContentReferenceOption>>.Ok(options);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new Result<IReadOnlyList<CmsContentReferenceOption>>.Failure(
                AeroError.DatabaseError("Posts could not be loaded for selection."));
        }
    }

    public async Task<Result<bool>> ExistsAsync(
        long siteId,
        long id,
        CancellationToken ct = default)
    {
        try
        {
            var post = await session.LoadAsync<PostDocument>(id, ct);
            return new Result<bool>.Ok(post is not null && post.SiteId == siteId);
        }
        catch (Exception)
        {
            return new Result<bool>.Failure(
                AeroError.DatabaseError("The selected post could not be verified."));
        }
    }
}
