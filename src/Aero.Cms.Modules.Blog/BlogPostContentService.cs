using Aero.Cms.Modules.Blog.Validators;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Pages.Validators;
using FlakeId;
using Marten;

namespace Aero.Cms.Modules.Blog;

public interface IBlogPostContentService
{
    Task<BlogPostDocument?> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<BlogPostDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BlogPostDocument>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default);
    Task SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default);
}

public sealed class MartenBlogPostContentService(IDocumentSession session) : IBlogPostContentService
{
    public Task<BlogPostDocument?> LoadAsync(long id, CancellationToken cancellationToken = default)
    {

        return session.LoadAsync<BlogPostDocument>(id, cancellationToken);
    }

    public async Task<BlogPostDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // var reservation = await session
        //     .LoadAsync<ContentSlugDocument>(ContentSlugDocument.BuildDocumentId(slug), cancellationToken);

        var reservation = await session.Query<ContentSlugDocument>()
            .FirstOrDefaultAsync(x => 
                string.Equals(slug, x.Slug, StringComparison.InvariantCultureIgnoreCase), token: cancellationToken);


        if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
        {
            return null;
        }

        return await session.LoadAsync<BlogPostDocument>(reservation.OwnerId, cancellationToken);
    }

    public async Task SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);
        var validator = new BlogPostValidator();
        var valid = await validator.ValidateAsync(post, cancellationToken);

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
    }

    public async Task<IReadOnlyList<BlogPostDocument>> GetLatestPostsAsync(int count, CancellationToken cancellationToken = default)
    {
        var latest = await session.Query<BlogPostDocument>()
            .Where(x => x.PublicationState == ContentPublicationState.Published)
            .OrderByDescending(x => x.PublishedOn)
            .Take(count)
            .ToListAsync(token: cancellationToken);

        return latest;
    }
}
