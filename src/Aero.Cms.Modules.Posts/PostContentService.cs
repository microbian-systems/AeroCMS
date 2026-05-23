using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using FlakeId;
using Marten;
using Marten.Pagination;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Cms.Modules.Posts.Models;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Posts;

public interface IPostContentService
{
    Task<Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<PostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<PostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default);
    Task<Result<PostDocument, AeroError>> SaveAsync(PostDocument post, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default);
    Task<Result<IPagedList<PostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Category>, AeroError>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<PostAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenBlogPostContentService(
    IDocumentSession session,
    ISiteContext siteContext,
    IMessageBus? bus = null,
    IHttpContextAccessor? httpContextAccessor = null,
    IFusionCache? cache = null) : IPostContentService
{
    private const string BlogCacheTag = "blog-index";
    private readonly ISiteContext _siteContext = siteContext;

    public async Task<Result<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"list:{skip}:{take}:{NormalizeCachePart(search)}");
            var cached = await TryGetCacheAsync<BlogPostListCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>((cached.Items, cached.TotalCount));
            }

            var query = session.Query<PostDocument>().Where(x => x.SiteId == _siteContext.SiteId);

            IQueryable<PostDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var posts = await ((global::Marten.Linq.IMartenQueryable<PostDocument>)filteredQuery)
                .OrderByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            await SetCacheAsync(cacheKey, new BlogPostListCacheEntry(posts.ToList(), stats.TotalResults), cancellationToken);
            return Prelude.Ok<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>((posts, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<PostDocument> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var post = await session.LoadAsync<PostDocument>(id, cancellationToken);
            if (post is null || post.SiteId != _siteContext.SiteId)
                return Prelude.Fail<bool, AeroError>(AeroError.CreateError($"Blog post with id '{id}' not found or access denied"));

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.BlogPost && x.SiteId == _siteContext.SiteId, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PostDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            await PublishContentUpdatedAsync(post, post.Slug, cancellationToken);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var cacheKey = BuildCacheKey($"id:{id}");
            var cached = await TryGetCacheAsync<PostDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PostDocument?, AeroError>(cached);
            }

            var document = await session.LoadAsync<PostDocument>(id, cancellationToken);
            if (document is null || document.SiteId != _siteContext.SiteId)
            {
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError($"Blog post with id '{id}' not found or access denied"));
            }

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"slug:{NormalizeCachePart(slug)}");
            var cached = await TryGetCacheAsync<PostDocument>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<PostDocument?, AeroError>(cached);
            }

            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    x.SiteId == _siteContext.SiteId &&
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);

            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
            {
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with slug '{slug}' not found"));
            }

            var document = await session.LoadAsync<PostDocument>(reservation.OwnerId, cancellationToken);
            if (document is null)
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with id '{reservation.OwnerId}' not found"));

            // Filter by published state — unpublished posts must not be publicly accessible
            if (document.PublicationState != ContentPublicationState.Published)
                return Prelude.Fail<PostDocument?, AeroError>(AeroError.NotFoundError($"Blog post with slug '{slug}' not found"));

            await SetCacheAsync(cacheKey, document, cancellationToken);
            return Prelude.Ok<PostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = BuildCacheKey($"latest:{count}");
            var cached = await TryGetCacheAsync<BlogPostCollectionCacheEntry>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(cached.Items);
            }

            var latest = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Take(count)
                .ToListAsync(token: cancellationToken);

            await SetCacheAsync(cacheKey, new BlogPostCollectionCacheEntry(latest.ToList()), cancellationToken);
            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(latest);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PostDocument, AeroError>> SaveAsync(PostDocument post, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(post);
            ValidateId(post.Id);

            var existingPost = await session.LoadAsync<PostDocument>(post.Id, cancellationToken);
            // Only stamp SiteId from context when not already set by the caller (e.g. seed).
            if (existingPost is null && post.SiteId == 0)
                post.SiteId = _siteContext.SiteId;
            await ContentSlugReservation.ReserveAsync(
                session,
                post.Id,
                ContentSlugOwnerType.BlogPost,
                post.Slug,
                post.SiteId,
                existingPost?.Slug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedAtUtc = existingPost?.CreatedOn;
            post.CreatedOn = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
            post.ModifiedOn = now;
            post.ModifiedBy = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            post.PublishedOn = post.PublicationState == ContentPublicationState.Published
                ? existingPost?.PublishedOn ?? now
                : null;

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);
            await PublishContentUpdatedAsync(post, existingPost?.Slug, cancellationToken);

            return Prelude.Ok<PostDocument, AeroError>(post);
        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .Where(x => x.TagIds.Contains(tagId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<PostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .Where(x => x.CategoryIds.Contains(categoryId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<PostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IPagedList<PostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var pagedList = await session.Query<PostDocument>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Skip(skip)
                .ToPagedListAsync(pageNumber, pageSize, cancellationToken);

            return Prelude.Ok<IPagedList<PostDocument>, AeroError>(pagedList);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IPagedList<PostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await session.Query<Tag>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Name)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<Tag>, AeroError>(tags);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<Tag>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Category>, AeroError>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await session.Query<Category>()
                .Where(x => x.SiteId == _siteContext.SiteId)
                .OrderBy(x => x.Name)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<Category>, AeroError>(categories);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<Category>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<PostAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(authorId);
            var author = await session.LoadAsync<PostAuthor>(authorId, cancellationToken);
            return author is null
                ? Prelude.Fail<PostAuthor?, AeroError>(AeroError.CreateError($"Author with id '{authorId}' not found"))
                : Prelude.Ok<PostAuthor?, AeroError>(author);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<PostAuthor?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    private static void ValidateId(long id)
    {
        var snowflake = Id.Parse(id);
    }

    private string BuildCacheKey(string suffix)
        => $"cms:posts:{_siteContext.SiteId}:{suffix}";

    private async Task<T?> TryGetCacheAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        if (cache is null)
        {
            return null;
        }

        var cached = await cache.TryGetAsync<T>(key, token: cancellationToken);
        return cached.HasValue ? cached.Value : null;
    }

    private Task SetCacheAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
        => cache is null
            ? Task.CompletedTask
            : cache.SetAsync(key, value, tags: [BlogCacheTag], token: cancellationToken).AsTask();

    private Task PublishContentUpdatedAsync(PostDocument post, string? oldSlug, CancellationToken cancellationToken)
        => bus is null
            ? Task.CompletedTask
            : bus.PublishAsync(new BlogPostContentUpdatedEvent(post.Id, post.SiteId, post.Slug, oldSlug)).AsTask();

    private static string NormalizeCachePart(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().Trim('/').ToLowerInvariant();

    private sealed record BlogPostListCacheEntry(List<PostDocument> Items, long TotalCount);
    private sealed record BlogPostCollectionCacheEntry(List<PostDocument> Items);
}
