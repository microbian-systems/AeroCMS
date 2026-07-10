using Aero.Cms.Abstractions.Content;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;

namespace Aero.Cms.Core.Content.Services;

/// <summary>
/// Represents a class for AeroContentService.
/// </summary>
public sealed class AeroContentService(IDocumentSession session) : IContentService
{
        /// <summary>
    /// LoadAsync method.
    /// </summary>
public async Task<Result<ContentItem, AeroError>> LoadAsync(long id, CancellationToken ct = default)
    {
        var item = await session.LoadAsync<ContentItem>(id, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item '{id}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

        /// <summary>
    /// GetBySlugAsync method.
    /// </summary>
public async Task<Result<ContentItem, AeroError>> GetBySlugAsync(long siteId, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>().FirstOrDefaultAsync(x => x.SiteId == siteId && x.Slug == slug, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item with slug '{slug}' not found."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

        /// <summary>
    /// GetBySlugAndTypeAsync method.
    /// </summary>
public async Task<Result<ContentItem, AeroError>> GetBySlugAndTypeAsync(long siteId, string contentTypeAlias, string slug, CancellationToken ct = default)
    {
        var item = await session.Query<ContentItem>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.ContentTypeAlias == contentTypeAlias && x.Slug == slug, ct);
        return item is null
            ? Prelude.Fail<ContentItem, AeroError>(AeroError.CreateError($"Content item with slug '{slug}' not found in type '{contentTypeAlias}'."))
            : Prelude.Ok<ContentItem, AeroError>(item);
    }

        /// <summary>
    /// SaveAsync method.
    /// </summary>
public async Task<Result<ContentItem, AeroError>> SaveAsync(ContentItem item, CancellationToken ct = default)
    {
        session.Store(item);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<ContentItem, AeroError>(item);
    }

        /// <summary>
    /// ExistsAsync method.
    /// </summary>
public async Task<bool> ExistsAsync(long id, CancellationToken ct = default)
        => await session.LoadAsync<ContentItem>(id, ct) is not null;

        /// <summary>
    /// DeleteAsync method.
    /// </summary>
public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        session.Delete<ContentItem>(id);
        await session.SaveChangesAsync(ct);
        return Prelude.Ok<bool, AeroError>(true);
    }
}
