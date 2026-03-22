namespace Aero.Cms.Modules.Pages;

using Aero.Core.Railway;

public interface IPageContentService
{
    Task<Result<string, PageDocument?>> LoadAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> LoadHomepageAsync(CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument?>> LoadBlogListingAsync(CancellationToken cancellationToken = default);
    Task<Result<string, IReadOnlyList<PageDocument>>> GetAllPagesAsync(CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(IDocumentSession session) : IPageContentService
{
    public async Task<Result<string, PageDocument?>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            ValidateId(id);
            var document = await session.LoadAsync<PageDocument>(id, cancellationToken);
            return document is null
                ? Prelude.Fail<string, PageDocument?>($"Page with id '{id}' not found")
                : Prelude.Ok<string, PageDocument?>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument?>(ex.Message);
        }
    }

    public Task<Result<string, PageDocument?>> LoadHomepageAsync(CancellationToken cancellationToken = default)
        => LoadAsync(PageDocumentIds.Homepage, cancellationToken);

    public Task<Result<string, PageDocument?>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => LoadAsync(PageDocumentIds.BlogListing, cancellationToken);

    public async Task<Result<string, IReadOnlyList<PageDocument>>> GetAllPagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pages = await session.Query<PageDocument>()
                .OrderBy(x => x.Title)
                .ToListAsync(token: cancellationToken);

            return Prelude.Ok<string, IReadOnlyList<PageDocument>>(pages);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, IReadOnlyList<PageDocument>>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument?>> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x =>
                    string.Equals(slug, x.Slug, StringComparison.CurrentCultureIgnoreCase), token: cancellationToken);
            if (reservation is null || reservation.OwnerType != ContentSlugOwnerType.Page)
            {
                return Prelude.Fail<string, PageDocument?>($"Page with slug '{slug}' not found");
            }

            var document = await session.LoadAsync<PageDocument>(reservation.OwnerId, cancellationToken);
            return document is null
                ? Prelude.Fail<string, PageDocument?>($"Page with id '{reservation.OwnerId}' not found")
                : Prelude.Ok<string, PageDocument?>(document);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument?>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        try
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
            var existingCreatedAtUtc = existingPage?.CreatedOn;
            page.CreatedOn = existingCreatedAtUtc is null || existingCreatedAtUtc == default ? now : existingCreatedAtUtc.Value;
            page.ModifiedOn = now;
            page.PublishedOn = page.PublicationState == ContentPublicationState.Published
                ? existingPage?.PublishedOn ?? now
                : null;

            session.Store(page);
            await session.SaveChangesAsync(cancellationToken);

            return Prelude.Ok<string, PageDocument>(page);
        }
        catch (ArgumentException ex)
        {
            return Prelude.Fail<string, PageDocument>(ex.Message);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, PageDocument>(ex.Message);
        }
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
            !string.Equals(page.Id.ToString(), PageDocumentIds.Homepage.ToString(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Homepage must use the stable id '{PageDocumentIds.Homepage}'.", nameof(page));
        }

        if (page.Kind == PageKind.BlogListing &&
            !string.Equals(page.Id.ToString(), PageDocumentIds.BlogListing.ToString(), StringComparison.Ordinal))
        {
            throw new ArgumentException($"Blog listing page must use the stable id '{PageDocumentIds.BlogListing}'.", nameof(page));
        }
    }

    private static void ValidateId(long id)
    {
        var snowflake = Id.Parse(id);
    }
}
