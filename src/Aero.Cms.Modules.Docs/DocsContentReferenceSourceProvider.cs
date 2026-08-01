using System.Globalization;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Modules.Docs;

/// <summary>Exposes current-site documentation to dynamic CMS reference fields.</summary>
public sealed class DocsContentReferenceSourceProvider(IQuerySession session)
    : IContentReferenceSourceProvider
{
    public string SourceKey => CmsContentReferenceSources.Docs;
    public string DisplayName => "Docs";

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
            IQueryable<DocsPage> query;
            if (requestedCulture is { Length: > 0 }
                && term is { Length: > 0 })
            {
                query = session.Query<DocsPage>().Where(document =>
                    document.SiteId == siteId
                    && document.Culture == requestedCulture
                    && (document.Title.ToLower().Contains(term)
                        || document.Slug.ToLower().Contains(term)
                        || (document.Summary != null
                            && document.Summary.ToLower().Contains(term))));
            }
            else if (requestedCulture is { Length: > 0 })
            {
                query = session.Query<DocsPage>().Where(document =>
                    document.SiteId == siteId
                    && document.Culture == requestedCulture);
            }
            else if (term is { Length: > 0 })
            {
                query = session.Query<DocsPage>().Where(document =>
                    document.SiteId == siteId
                    && (document.Title.ToLower().Contains(term)
                        || document.Slug.ToLower().Contains(term)
                        || (document.Summary != null
                            && document.Summary.ToLower().Contains(term))));
            }
            else
            {
                query = session.Query<DocsPage>().Where(document =>
                    document.SiteId == siteId);
            }

            var docs = await ((ISableQueryable<DocsPage>)query)
                .OrderBy(document => document.Title)
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            var options = docs
                .Select(document => new CmsContentReferenceOption(
                    document.Id.ToString(CultureInfo.InvariantCulture),
                    document.Title,
                    document.Slug,
                    document.Culture))
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
                AeroError.DatabaseError("Docs could not be loaded for selection."));
        }
    }

    public async Task<Result<bool>> ExistsAsync(
        long siteId,
        long id,
        CancellationToken ct = default)
    {
        try
        {
            var document = await session.LoadAsync<DocsPage>(id, ct);
            return new Result<bool>.Ok(
                document is not null && document.SiteId == siteId);
        }
        catch (Exception)
        {
            return new Result<bool>.Failure(
                AeroError.DatabaseError("The selected documentation item could not be verified."));
        }
    }
}
