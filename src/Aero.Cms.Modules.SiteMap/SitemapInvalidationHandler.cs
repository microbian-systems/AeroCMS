using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Modules.Docs;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.SiteMap;

/// <summary>
/// Marten document session listener that invalidates the site-scoped sitemap cache
/// whenever content documents are created, updated, or deleted.
/// </summary>
public sealed class SitemapCacheListener : DocumentSessionListenerBase
{
    private readonly IFusionCache _cache;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SitemapCacheListener> _logger;

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

            changes.InsertsFor<BlogPostDocument>().Any() ||
            changes.UpdatesFor<BlogPostDocument>().Any() ||
            changes.DeletionsFor<BlogPostDocument>().Any() ||

            changes.InsertsFor<DocsPage>().Any() ||
            changes.UpdatesFor<DocsPage>().Any() ||
            changes.DeletionsFor<DocsPage>().Any())
        {
            _logger.LogDebug("Content change detected — invalidating sitemap cache by tag");
            await _cache.RemoveByTagAsync("sitemap", token: token);
        }
    }
}
