using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using Marten;

namespace Aero.Cms.Core.Content.Services;

public sealed class MartenContentQueryService(IDocumentSession session) : IContentQueryService
{
    public async Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId, string alias, int skip, int take, CancellationToken ct)
    {
        var query = session.Query<ContentItem>().Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).OrderByDescending(x => x.PublishedOn ?? DateTimeOffset.MinValue).ToListAsync(ct);
        return Prelude.Ok<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>((items, total));
    }

    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId, string alias, Dictionary<string, string> filters, CancellationToken ct)
    {
        var query = session.Query<ContentItem>().Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);
        var items = await query.OrderByDescending(x => x.PublishedOn ?? DateTimeOffset.MinValue).ToListAsync(ct);
        return Prelude.Ok<IReadOnlyList<ContentItem>, AeroError>((IReadOnlyList<ContentItem>)items);
    }
}
