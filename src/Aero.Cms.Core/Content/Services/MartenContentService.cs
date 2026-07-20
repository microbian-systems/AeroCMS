using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using System.Globalization;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Implements <see cref="IContentService"/> with a Sable document session.
/// </summary>
public sealed class AeroContentService(IDocumentSession session) : IContentService
{
    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> LoadAsync(long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item with slug '{slug}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(
    long siteId,
    string contentTypeAlias,
    string culture,
    string slug,
    CancellationToken ct = default)
    {
        var normalizedCulture = NormalizeCulture(culture);
        var item = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(x =>
                x.SiteId == siteId &&
                x.ContentTypeAlias == contentTypeAlias &&
                x.Culture == normalizedCulture &&
                x.Slug == slug,
                ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError(
                $"Content item with slug '{slug}' and culture '{normalizedCulture}' not found in type '{contentTypeAlias}'."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default)
    {
        session.Store(item);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<ContentItem, AeroError>(item);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long id, CancellationToken ct = default)
        => await session.LoadAsync<ContentItem>(id, ct) is not null;

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<ContentItem>(id);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }

    private static string NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture)
            ? "en-US"
            : CultureInfo.GetCultureInfo(culture.Trim()).Name;
}
