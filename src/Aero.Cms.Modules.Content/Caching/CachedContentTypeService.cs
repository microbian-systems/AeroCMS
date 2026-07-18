using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Read-through cache decorator for content-type definitions.
/// </summary>
internal sealed class CachedContentTypeService(
    AeroContentTypeService inner,
    IFusionCache cache,
    ContentCacheInvalidator invalidator,
    ILogger<CachedContentTypeService> logger) : IContentTypeService
{
    public async Task<Result<ContentTypeDefinition, AeroError>> GetByAliasAsync(
        long siteId,
        string alias,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.TypeByAlias(siteId, alias);
        var cached = await cache.TryGetAsync<ContentTypeDefinition>(key, token: ct);
        if (cached.HasValue)
        {
            return Prelude.Ok<ContentTypeDefinition, AeroError>(
                ContentCacheSnapshot.Clone(cached.Value));
        }

        var result = await inner.GetByAliasAsync(siteId, alias, ct);
        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<ContentTypeDefinition>, AeroError>> GetAllAsync(
        long siteId,
        CancellationToken ct = default)
    {
        var key = ContentCacheKeys.TypeList(siteId);
        var cached = await cache.TryGetAsync<ContentTypeListCacheEntry>(key, token: ct);
        if (cached.HasValue)
        {
            return Prelude.Ok<IReadOnlyList<ContentTypeDefinition>, AeroError>(
                cached.Value.Items.Select(ContentCacheSnapshot.Clone).ToList());
        }

        var result = await inner.GetAllAsync(siteId, ct);
        if (result is Result<IReadOnlyList<ContentTypeDefinition>, AeroError>.Ok ok)
        {
            await BestEffortAsync(
                () => cache.SetAsync(
                    key,
                    new ContentTypeListCacheEntry(
                        ok.Value.Select(ContentCacheSnapshot.Clone).ToList()),
                    tags: [ContentCacheKeys.ContentTypesTag(siteId)],
                    token: ct).AsTask());
        }

        return result;
    }

    public async Task<Result<ContentTypeDefinition, AeroError>> SaveAsync(
        ContentTypeDefinition definition,
        CancellationToken ct = default)
    {
        var result = await inner.SaveAsync(definition, ct);
        if (result is Result<ContentTypeDefinition, AeroError>.Ok ok)
        {
            await invalidator.InvalidateTypeAsync(ok.Value.SiteId, ok.Value.Alias);
            await SetAsync(ok.Value, ct);
        }

        return result;
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(
        long siteId,
        string alias,
        CancellationToken ct = default)
    {
        var result = await inner.DeleteAsync(siteId, alias, ct);
        if (result is Result<bool, AeroError>.Ok { Value: true })
        {
            await invalidator.InvalidateTypeAsync(siteId, alias);
        }

        return result;
    }

    private Task SetAsync(ContentTypeDefinition definition, CancellationToken ct) =>
        BestEffortAsync(async () =>
        {
            var snapshot = ContentCacheSnapshot.Clone(definition);
            var tags = new[]
            {
                ContentCacheKeys.ContentTypesTag(definition.SiteId),
                ContentCacheKeys.ContentTypeTag(definition.SiteId, definition.Alias)
            };
            await cache.SetAsync(
                ContentCacheKeys.TypeById(definition.SiteId, definition.Id),
                snapshot,
                tags: tags,
                token: ct);
            await cache.SetAsync(
                ContentCacheKeys.TypeByAlias(definition.SiteId, definition.Alias),
                snapshot,
                tags: tags,
                token: ct);
        });

    private async Task BestEffortAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Content-type cache update did not complete.");
        }
    }

    private sealed record ContentTypeListCacheEntry(List<ContentTypeDefinition> Items);
}
