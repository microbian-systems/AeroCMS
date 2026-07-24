using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentQueryService"/> with Sable queries.
/// </summary>
public sealed class AeroContentQueryService(IDocumentSession session) : IContentQueryService
{
    /// <inheritdoc />
    public async Task<Result<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>> GetByTypeAsync(
        long siteId, string alias, int skip, int take, CancellationToken ct)
    {
        var query = session.Query<ContentItem>().Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.PublishedOn).Skip(skip).Take(take).ToListAsync(ct);
        return Prelude.Ok<(IReadOnlyList<ContentItem> Items, long TotalCount), AeroError>((items, total));
    }

    /// <inheritdoc />
    public async Task<Result<long, AeroError>> CountByTypeAsync(
        long siteId, string alias, CancellationToken ct = default)
    {
        var count = await session.Query<ContentItem>()
            .Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias)
            .CountAsync(ct);

        return Prelude.Ok<long, AeroError>(count);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> SearchAsync(
        long siteId, string alias, Dictionary<string, string> filters, CancellationToken ct)
    {
        var query = session.Query<ContentItem>().Where(x => x.SiteId == siteId && x.ContentTypeAlias == alias);
        var items = await query.OrderByDescending(x => x.PublishedOn).ToListAsync(ct);
        if (filters.TryGetValue("__search", out var search) && !string.IsNullOrWhiteSpace(search))
        {
            items = items
                .Where(x =>
                    Contains(x.Title, search) ||
                    Contains(x.Slug, search) ||
                    x.Fields.Values.Any(v => Contains(v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(), search)))
                .ToList();
        }

        return Prelude.Ok<IReadOnlyList<ContentItem>, AeroError>((IReadOnlyList<ContentItem>)items);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ContentItem>, AeroError>> ListCultureVariantsAsync(
        long siteId, string alias, long translationGroupId, CancellationToken ct = default)
    {
        var items = await session.Query<ContentItem>()
            .Where(x =>
                x.SiteId == siteId &&
                x.ContentTypeAlias == alias &&
                (x.TranslationGroupId == translationGroupId || x.Id == translationGroupId))
            .OrderBy(x => x.Culture)
            .ToListAsync(ct);

        return Prelude.Ok<IReadOnlyList<ContentItem>, AeroError>(items);
    }

    private static bool Contains(string? value, string search)
        => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
