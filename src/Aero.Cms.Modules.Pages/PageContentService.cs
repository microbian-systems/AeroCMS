using Aero.Cms.Core;
using Aero.Cms.Modules.Pages.Validators;
using Aero.Core;

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
    Task<Result<string, PageDocument>> CreateAsync(Requests.CreatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<string, PageDocument>> UpdateAsync(long id, Requests.UpdatePageRequest request, CancellationToken cancellationToken = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class MartenPageContentService(IDocumentSession session) : IPageContentService
{
    public async Task<Result<string, PageDocument?>> LoadAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
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
        => FindBySlugAsync("/", cancellationToken);

    public Task<Result<string, PageDocument?>> LoadBlogListingAsync(CancellationToken cancellationToken = default)
        => FindBySlugAsync("blog", cancellationToken);

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

    public async Task<Result<string, PageDocument>> CreateAsync(Requests.CreatePageRequest request, CancellationToken cancellationToken = default)
    {
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            Title = request.Title,
            Slug = request.Slug,
            Summary = request.Summary,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            PublicationState = request.PublicationState
        };

        return await SaveAsync(page, cancellationToken);
    }

    public async Task<Result<string, PageDocument>> UpdateAsync(long id, Requests.UpdatePageRequest request, CancellationToken cancellationToken = default)
    {
        var loadResult = await LoadAsync(id, cancellationToken);
        if (loadResult is Result<string, PageDocument?>.Ok { Value: not null } ok)
        {
            var page = ok.Value;
            page.Title = request.Title;
            page.Slug = request.Slug;
            page.Summary = request.Summary;
            page.SeoTitle = request.SeoTitle;
            page.SeoDescription = request.SeoDescription;
            page.PublicationState = request.PublicationState;

            return await SaveAsync(page, cancellationToken);
        }

        return Prelude.Fail<string, PageDocument>($"Page with id '{id}' not found");
    }

    public async Task<Result<string, bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(x => x.OwnerId == id && x.OwnerType == ContentSlugOwnerType.Page, token: cancellationToken);

            if (reservation is not null)
            {
                session.Delete(reservation);
            }

            session.Delete<PageDocument>(id);
            await session.SaveChangesAsync(cancellationToken);
            return Prelude.Ok<string, bool>(true);
        }
        catch (Exception ex)
        {
            return Prelude.Fail<string, bool>(ex.Message);
        }
    }

    public async Task<Result<string, PageDocument>> SaveAsync(PageDocument page, CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(page);
            await ValidatePage(page);

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

    private static async Task ValidatePage(PageDocument page)
    {
        var validator = new PageModelValidator();
        var valid = await validator.ValidateAsync(page);

        if (valid.Errors.Any())
        {
            // todo - return a Result<T> here and avoid throwing an exception
            throw new ArgumentException($"page errors: {valid.Errors}");
        }
    }

}
