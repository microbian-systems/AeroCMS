using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Railway;
using FlakeId;
using Marten;
using Marten.Pagination;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aero.Cms.Modules.Blog;

public interface IBlogPostContentService
{
    Task<Result<(IReadOnlyList<BlogPostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<BlogPostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<BlogPostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default);
    Task<Result<BlogPostDocument, AeroError>> SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default);
    Task<Result<IPagedList<BlogPostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Category>, AeroError>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<BlogAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default);
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenBlogPostContentService(IDocumentSession session) : IBlogPostContentService
{
    public async Task<Result<(IReadOnlyList<BlogPostDocument> Items, long TotalCount), AeroError>> GetAllPostsAsync(int skip = 0, int take = 10, string? search = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = session.Query<BlogPostDocument>();

            IQueryable<BlogPostDocument> filteredQuery = query;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                filteredQuery = query.Where(x => x.Title.ToLower().Contains(s) || x.Slug.ToLower().Contains(s));
            }

            var stats = new global::Marten.Linq.QueryStatistics();
            var posts = await ((global::Marten.Linq.IMartenQueryable<BlogPostDocument>)filteredQuery)
                .OrderByDescending(x => x.CreatedOn)
                .Stats(out stats)
                .Skip(skip)
                .Take(take)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<(IReadOnlyList<BlogPostDocument> Items, long TotalCount), AeroError>((posts, stats.TotalResults));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<(IReadOnlyList<BlogPostDocument> Items, long TotalCount), AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.BlogPost, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<BlogPostDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<bool, AeroError>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<bool, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BlogPostDocument?, AeroError>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var document = await session.LoadAsync<BlogPostDocument>(id, cancellationToken);
            return document is null
                ? Prelude.Fail<BlogPostDocument?, AeroError>(AeroError.CreateError($"Blog post with id '{id}' not found"))
                : Prelude.Ok<BlogPostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BlogPostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BlogPostDocument?, AeroError>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);

            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
            {
                return Prelude.Fail<BlogPostDocument?, AeroError>(AeroError.CreateError($"Blog post with slug '{slug}' not found"));
            }

            var document = await session.LoadAsync<BlogPostDocument>(reservation.OwnerId, cancellationToken);
            return document is null
                ? Prelude.Fail<BlogPostDocument?, AeroError>(AeroError.CreateError($"Blog post with id '{reservation.OwnerId}' not found"))
                : Prelude.Ok<BlogPostDocument?, AeroError>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BlogPostDocument?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default)
    {
        try
        {
            var latest = await session.Query<BlogPostDocument>()
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Take(count)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<BlogPostDocument>, AeroError>(latest);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<BlogPostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BlogPostDocument, AeroError>> SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(post);
            ValidateId(post.Id);

            var existingPost = await session.LoadAsync<BlogPostDocument>(post.Id, cancellationToken);
            await ContentSlugReservation.ReserveAsync(
                session,
                post.Id,
                ContentSlugOwnerType.BlogPost,
                post.Slug,
                existingPost?.Slug,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var existingCreatedAtUtc = existingPost?.CreatedOn;
            post.CreatedOn = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
            post.ModifiedOn = now;
            post.PublishedOn = post.PublicationState == ContentPublicationState.Published
                ? existingPost?.PublishedOn ?? now
                : null;

            session.Store(post);
            await session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<BlogPostDocument, AeroError>(post);
        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<BlogPostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BlogPostDocument, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetByTagAsync(long tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<BlogPostDocument>()
                .Where(x => x.TagIds.Contains(tagId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<BlogPostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<BlogPostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<BlogPostDocument>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        try
        {
            var posts = await session.Query<BlogPostDocument>()
                .Where(x => x.CategoryIds.Contains(categoryId) && x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<BlogPostDocument>, AeroError>(posts);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<BlogPostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IPagedList<BlogPostDocument>, AeroError>> GetPagedPostsAsync(int pageNumber, int pageSize, int skip = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var pagedList = await session.Query<BlogPostDocument>()
                .Where(x => x.PublicationState == ContentPublicationState.Published)
                .OrderByDescending(x => x.PublishedOn)
                .Skip(skip)
                .ToPagedListAsync(pageNumber, pageSize, cancellationToken);

            return Prelude.Ok<IPagedList<BlogPostDocument>, AeroError>(pagedList);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IPagedList<BlogPostDocument>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Tag>, AeroError>> GetAllTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await session.Query<Tag>()
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
                .OrderBy(x => x.Name)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<IReadOnlyList<Category>, AeroError>(categories);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<IReadOnlyList<Category>, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    public async Task<Result<BlogAuthor?, AeroError>> GetAuthorAsync(long authorId, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(authorId);
            var author = await session.LoadAsync<BlogAuthor>(authorId, cancellationToken);
            return author is null
                ? Prelude.Fail<BlogAuthor?, AeroError>(AeroError.CreateError($"Author with id '{authorId}' not found"))
                : Prelude.Ok<BlogAuthor?, AeroError>(author);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<BlogAuthor?, AeroError>(AeroError.CreateError(ex.Message));
        }
    }

    private static void ValidateId(long id)
    {
        var snowflake = Id.Parse(id);
    }
}
