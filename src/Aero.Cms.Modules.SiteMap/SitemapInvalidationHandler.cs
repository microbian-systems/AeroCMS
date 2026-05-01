using Aero.Cms.Abstractions.Models;
using Marten;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Marten document session listener that invalidates the sitemap cache
/// whenever content documents are created, updated, or deleted.
/// 
/// Replaces the previous Wolverine handler approach which crashed due
/// to Wolverine's <c>GetPrettyName</c> failing on nested generic types
/// used as handler parameters (<c>AeroEvent&lt;T&gt;.PageCreated</c>, etc.).
/// </summary>
public sealed class SitemapCacheListener : DocumentSessionListenerBase
{
    private readonly IFusionCache _cache;
    private readonly ILogger<SitemapCacheListener> _logger;

    public SitemapCacheListener(IFusionCache cache, ILogger<SitemapCacheListener> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Called just before saving changes. Checks if any content document types
    /// have pending changes and invalidates the sitemap cache if so.
    /// </summary>
    public override async Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
    {
        var changes = session.PendingChanges;

        if (changes.InsertsFor<PageViewModel>().Any() ||
            changes.UpdatesFor<PageViewModel>().Any() ||
            changes.DeletionsFor<PageViewModel>().Any() ||

            changes.InsertsFor<PostViewModel>().Any() ||
            changes.UpdatesFor<PostViewModel>().Any() ||
            changes.DeletionsFor<PostViewModel>().Any() ||

            changes.InsertsFor<DocViewModel>().Any() ||
            changes.UpdatesFor<DocViewModel>().Any() ||
            changes.DeletionsFor<DocViewModel>().Any())
        {
            _logger.LogDebug("Content change detected — invalidating sitemap cache");
            await _cache.RemoveAsync("sitemap:xml", token: token);
        }
    }
}
