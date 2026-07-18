using Microsoft.AspNetCore.OutputCaching;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Performs best-effort post-commit invalidation of both document and rendered
/// response caches. Persistence success is never converted to failure by a
/// transient cache outage.
/// </summary>
internal sealed class ContentCacheInvalidator(
    IFusionCache cache,
    IOutputCacheStore outputCache,
    ILogger<ContentCacheInvalidator> logger)
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    public async Task InvalidateTypeAsync(
        long siteId,
        params string?[] aliases)
    {
        await BestEffortAsync(
            "FusionCache content-type list",
            cancellationToken => cache.RemoveByTagAsync(
                ContentCacheKeys.ContentTypesTag(siteId),
                token: cancellationToken));
        await BestEffortAsync(
            "output-cache public content",
            cancellationToken => outputCache.EvictByTagAsync(
                ContentCacheKeys.ContentPublicTag(siteId),
                cancellationToken));

        foreach (var alias in aliases.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var tag = ContentCacheKeys.ContentTypeTag(siteId, alias!);
            await BestEffortAsync(
                $"FusionCache content type '{alias}'",
                cancellationToken => cache.RemoveByTagAsync(
                    tag,
                    token: cancellationToken));
            await BestEffortAsync(
                $"output-cache content type '{alias}'",
                cancellationToken => outputCache.EvictByTagAsync(tag, cancellationToken));
        }
    }

    public async Task InvalidateItemAsync(
        ContentItemCacheIdentity? oldIdentity,
        ContentItemCacheIdentity? newIdentity)
    {
        foreach (var identity in new[] { oldIdentity, newIdentity }
                     .Where(static identity => identity is not null)
                     .Cast<ContentItemCacheIdentity>()
                     .Distinct())
        {
            var fusionTags = new[]
            {
                ContentCacheKeys.ContentItemsTag(identity.SiteId),
                ContentCacheKeys.ContentItemsByTypeTag(identity.SiteId, identity.TypeAlias),
                ContentCacheKeys.ContentItemTag(identity.SiteId, identity.ItemId),
                ContentCacheKeys.ContentItemSlugTag(
                    identity.SiteId,
                    identity.TypeAlias,
                    identity.Culture,
                    identity.Slug)
            };

            foreach (var tag in fusionTags)
            {
                await BestEffortAsync(
                    $"FusionCache tag '{tag}'",
                    cancellationToken => cache.RemoveByTagAsync(
                        tag,
                        token: cancellationToken));
            }

            var outputTags = new[]
            {
                ContentCacheKeys.ContentItemTag(identity.SiteId, identity.ItemId),
                ContentCacheKeys.ContentItemSlugTag(
                    identity.SiteId,
                    identity.TypeAlias,
                    identity.Culture,
                    identity.Slug),
                ContentCacheKeys.ContentTypeTag(identity.SiteId, identity.TypeAlias)
            };

            foreach (var tag in outputTags)
            {
                await BestEffortAsync(
                    $"output-cache tag '{tag}'",
                    cancellationToken => outputCache.EvictByTagAsync(tag, cancellationToken));
            }
        }
    }

    private async Task BestEffortAsync(
        string operation,
        Func<CancellationToken, ValueTask> action)
    {
        using var timeout = new CancellationTokenSource(OperationTimeout);
        try
        {
            await action(timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Content was persisted, but cache invalidation operation {Operation} did not complete.",
                operation);
        }
    }
}

internal sealed record ContentItemCacheIdentity(
    long SiteId,
    long ItemId,
    string TypeAlias,
    string Culture,
    string Slug);
