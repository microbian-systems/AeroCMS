using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// AeroDB document session listener that invalidates the site-scoped sitemap cache
/// whenever content documents are created, updated, or deleted.
/// </summary>
public sealed class SitemapCacheListener : DocumentSessionListenerBase
{
    private readonly IFusionCache _cache;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SitemapCacheListener> _logger;

        /// <summary>
    /// Initializes the listener with its cache, environment, and logger dependencies.
    /// </summary>
    /// <param name="cache">The cache whose sitemap-tagged entries are invalidated.</param>
    /// <param name="environment">The environment used to disable invalidation outside production.</param>
    /// <param name="logger">The logger for invalidation diagnostics.</param>
public SitemapCacheListener(IFusionCache cache, IHostEnvironment environment, ILogger<SitemapCacheListener> logger)
    {
        _cache = cache;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Called just before saving changes. Checks if any content document types
    /// have pending changes and invalidates the sitemap cache by tag if so.
    /// The cache key is site-scoped (<c>sitemap:xml:{siteId}</c>) and tagged "sitemap".
    /// </summary>
    /// <param name="session">The session whose pending changes are inspected.</param>
    /// <param name="token">The token used for distributed cache invalidation.</param>
    /// <remarks>
    /// Invalidation removes every entry tagged <c>sitemap</c>, not only the current site's entries,
    /// and it occurs before the document commit. A later commit failure can therefore evict valid
    /// cache data, while cache-removal failures can prevent the save pipeline from continuing.
    /// </remarks>
    public override async Task BeforeSaveChangesAsync(IDocumentSession session, CancellationToken token)
    {
        if (!_environment.IsProduction())
        {
            return;
        }

        var changes = session.PendingChanges;

        if (changes.InsertsFor<PageDocument>().Any() ||
            changes.UpdatesFor<PageDocument>().Any() ||
            changes.DeletionsFor<PageDocument>().Any() ||

            changes.InsertsFor<PostDocument>().Any() ||
            changes.UpdatesFor<PostDocument>().Any() ||
            changes.DeletionsFor<PostDocument>().Any() ||

            changes.InsertsFor<DocsPage>().Any() ||
            changes.UpdatesFor<DocsPage>().Any() ||
            changes.DeletionsFor<DocsPage>().Any())
        {
            _logger.LogDebug("Content change detected — invalidating sitemap cache by tag");
            await _cache.RemoveByTagAsync("sitemap", token: token);
        }
    }
}
