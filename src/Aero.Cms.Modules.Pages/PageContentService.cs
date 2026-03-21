using Marten;

namespace Aero.Cms.Modules.Pages;

public interface IPageContentService
{
    Task<PageDocument?> LoadAsync(string id, CancellationToken cancellationToken = default);
    Task<PageDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<PageDocument?> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<PageDocument?> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(IDocumentSession session) : IPageContentService
{
    public Task<PageDocument?> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);
        return session.LoadAsync<PageDocument>(id, cancellationToken);
    }

    public Task<PageDocument?> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => LoadAsync(PageDocumentIds.Homepage, cancellationToken);

    public Task<PageDocument?> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => LoadAsync(PageDocumentIds.BlogListing, cancellationToken);

    public async Task<PageDocument?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var reservation = await session.LoadAsync<ContentSlugDocument>(ContentSlugDocument.BuildDocumentId(slug), cancellationToken);
        if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.Page)
        {
            return null;
        }

        return await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
    }

    public async Task SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ValidatePage(page);

        var existingPage = await session.LoadAsync<PageDocument>(page.Id, cancellationToken);
        await ContentSlugReservation.ReserveAsync(
            session,
            page.Id,
            ContentSlugOwnerType.Page,
            page.Slug,
            existingPage?.Slug,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var existingCreatedAtUtc = existingPage?.CreatedAtUtc;
        page.CreatedAtUtc = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
        page.UpdatedAtUtc = now;
        page.PublishedAtUtc = page.PublicationState == ContentPublicationState.Published
            ? existingPage?.PublishedAtUtc ?? now
            : null;

        session.Store(page);
        await session.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePage(PageDocument page)
    {
        ValidateId(page.Id);

        if (string.IsNullOrWhiteSpace(page.Title))
        {
            throw new ArgumentException("Page title is required.", nameof(page));
        }

        var normalizedSlug = ContentSlugDocument.Normalize(page.Slug);
        if (page.Kind != PageKind.Homepage &&
            string.IsNullOrWhiteSpace(normalizedSlug) &&
            !string.Equals(page.Slug, "/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Page slug is required.", nameof(page));
        }

        if (page.Kind == PageKind.Homepage &&
            !string.Equals(page.Id, PageDocumentIds.Homepage, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Homepage must use the stable id '{PageDocumentIds.Homepage}'.", nameof(page));
        }

        if (page.Kind == PageKind.BlogListing &&
            !string.Equals(page.Id, PageDocumentIds.BlogListing, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Blog listing page must use the stable id '{PageDocumentIds.BlogListing}'.", nameof(page));
        }
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Stable content ids are required.", nameof(id));
        }
    }
}
