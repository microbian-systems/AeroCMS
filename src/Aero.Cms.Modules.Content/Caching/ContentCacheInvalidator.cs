using Microsoft.AspNetCore.OutputCaching;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Content.Caching;

/// <summary>
/// Performs best-effort post-commit invalidation of both document and rendered
/// response caches. Persistence success is never converted to failure by a
/// transient cache outage.
/// </summary>
/// <param name="cache">The FusionCache whose tagged document snapshots are removed.</param>
/// <param name="outputCache">The output-cache store whose rendered responses are evicted.</param>
/// <param name="logger">The logger for suppressed cache failures.</param>
internal sealed class ContentCacheInvalidator(
    IFusionCache cache,
    IOutputCacheStore outputCache,
    ILogger<ContentCacheInvalidator> logger)
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Invalidates the site's content-type list, public output, and each nonblank alias.
    /// </summary>
    /// <param name="siteId">The site whose tags are evicted.</param>
    /// <param name="aliases">Aliases whose type-specific Fusion and output-cache tags are evicted.</param>
    /// <remarks>Each cache operation receives its own five-second timeout and failures are suppressed.</remarks>
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

    /// <summary>
    /// Invalidates cache tags for the distinct old and new identities of an item.
    /// </summary>
    /// <param name="oldIdentity">The identity before a mutation, or null for a create.</param>
    /// <param name="newIdentity">The identity after a mutation, or null for a delete.</param>
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
                ContentCacheKeys.ContentTranslationGroupTag(identity.SiteId, identity.TranslationGroupId),
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

    /// <summary>
    /// Reliably invalidates the tags affected by a shared-field generation. Unlike ordinary
    /// post-commit invalidation, the caller receives failure so durable repair work can retry.
    /// </summary>
    public async Task<bool> TryInvalidateTranslationGroupAsync(
        long siteId,
        long translationGroupId,
        string typeAlias,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OperationTimeout);
            await cache.RemoveByTagAsync(
                ContentCacheKeys.ContentTranslationGroupTag(siteId, translationGroupId),
                token: timeout.Token);
            await outputCache.EvictByTagAsync(
                ContentCacheKeys.ContentTypeTag(siteId, typeAlias),
                timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,
                "Localized content projection cache invalidation did not complete and will be retried.");
            return false;
        }
    }

    /// <summary>
    /// Executes one cache operation with an independent timeout and suppresses every exception.
    /// </summary>
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

/// <summary>
/// Captures all site, item, type, culture, and slug dimensions used in cache tags.
/// </summary>
internal sealed record ContentItemCacheIdentity(
    long SiteId,
    long ItemId,
    string TypeAlias,
    string Culture,
    string Slug,
    long TranslationGroupId);
