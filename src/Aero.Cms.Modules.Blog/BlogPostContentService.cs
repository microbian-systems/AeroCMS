using Aero.Cms.Modules.Pages;
using Marten;

namespace Aero.Cms.Modules.Blog;

public interface IBlogPostContentService
{
    Task<BlogPostDocument?> LoadAsync(string id, CancellationToken cancellationToken = default);
    Task<BlogPostDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default);
}

public sealed class MartenBlogPostContentService(IDocumentSession session) : IBlogPostContentService
{
    public Task<BlogPostDocument?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        return session.LoadAsync<BlogPostDocument>(id, cancellationToken);
    }

    public async Task<BlogPostDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var reservation = await session.LoadAsync<ContentSlugDocument>(ContentSlugDocument.BuildDocumentId(slug), cancellationToken);
        if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.BlogPost)
        {
            return null;
        }

        return await session.LoadAsync<BlogPostDocument>(reservation.OwnerId, cancellationToken);
    }

    public async Task SaveAsync(BlogPostDocument post, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(post);
        Validate(post);

        var existingPost = await session.LoadAsync<BlogPostDocument>(post.Id, cancellationToken);
        await ContentSlugReservation.ReserveAsync(
            session,
            post.Id,
            ContentSlugOwnerType.BlogPost,
            post.Slug,
            existingPost?.Slug,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var existingCreatedAtUtc = existingPost?.CreatedAtUtc;
        post.CreatedAtUtc = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
        post.UpdatedAtUtc = now;
        post.PublishedAtUtc = post.PublicationState == ContentPublicationState.Published
            ? existingPost?.PublishedAtUtc ?? now
            : null;

        session.Store(post);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(BlogPostDocument post)
    {
        ValidateId(post.Id);

        if (string.IsNullOrWhiteSpace(post.Title))
        {
            throw new ArgumentException("Blog post title is required.", nameof(post));
        }

        if (string.IsNullOrWhiteSpace(ContentSlugDocument.Normalize(post.Slug)))
        {
            throw new ArgumentException("Blog post slug is required.", nameof(post));
        }
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Stable blog post ids are required.", nameof(id));
        }
    }
}
