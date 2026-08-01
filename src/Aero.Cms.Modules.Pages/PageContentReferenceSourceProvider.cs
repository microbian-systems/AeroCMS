using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Pages;

/// <summary>Exposes current-site pages to dynamic CMS reference fields.</summary>
public sealed class PageContentReferenceSourceProvider(IQuerySession session)
    : IContentReferenceSourceProvider
{
    public string SourceKey => CmsContentReferenceSources.Pages;
    public string DisplayName => "Pages";

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
            IQueryable<PageDocument> query;
            if (requestedCulture is { Length: > 0 }
                && term is { Length: > 0 })
            {
                query = session.Query<PageDocument>().Where(page =>
                    page.SiteId == siteId
                    && page.Culture == requestedCulture
                    && (page.Title.ToLower().Contains(term)
                        || page.Slug.ToLower().Contains(term)
                        || page.Path.ToLower().Contains(term)));
            }
            else if (requestedCulture is { Length: > 0 })
            {
                query = session.Query<PageDocument>().Where(page =>
                    page.SiteId == siteId
                    && page.Culture == requestedCulture);
            }
            else if (term is { Length: > 0 })
            {
                query = session.Query<PageDocument>().Where(page =>
                    page.SiteId == siteId
                    && (page.Title.ToLower().Contains(term)
                        || page.Slug.ToLower().Contains(term)
                        || page.Path.ToLower().Contains(term)));
            }
            else
            {
                query = session.Query<PageDocument>().Where(page =>
                    page.SiteId == siteId);
            }

            var pages = await ((ISableQueryable<PageDocument>)query)
                .OrderBy(page => page.Title)
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            var options = pages
                .Select(page => new CmsContentReferenceOption(
                    page.Id.ToString(CultureInfo.InvariantCulture),
                    page.Title,
                    page.Slug,
                    page.Culture))
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
                AeroError.DatabaseError("Pages could not be loaded for selection."));
        }
    }

    public async Task<Result<bool>> ExistsAsync(
        long siteId,
        long id,
        CancellationToken ct = default)
    {
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, ct);
            return new Result<bool>.Ok(page is not null && page.SiteId == siteId);
        }
        catch (Exception)
        {
            return new Result<bool>.Failure(
                AeroError.DatabaseError("The selected page could not be verified."));
        }
    }
}
