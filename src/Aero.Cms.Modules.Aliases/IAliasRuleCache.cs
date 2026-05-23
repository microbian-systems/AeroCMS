namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Hot in-memory alias lookup. Zero database I/O per request.
/// Cache is refreshed once on startup via <see cref="AliasRuleCacheWarmupService"/>
/// and invalidated when aliases are created, updated, or deleted.
/// Lookups are site-scoped: the same old path on two different sites returns
/// different entries.
/// </summary>
public interface IAliasRuleCache
{
    /// <summary>
    /// Finds an alias rule matching the normalized old path for the given site.
    /// Returns null if no match. O(1) dictionary lookup.
    /// </summary>
    AliasRuleEntry? Find(long siteId, string oldPath);

    /// <summary>
    /// Refreshes the cache from the database. Called on startup and
    /// after alias mutations.
    /// </summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates the in-memory cache. The next request triggers a refresh.
    /// </summary>
    void Invalidate();
}

/// <summary>
/// Immutable snapshot of an alias rule in the hot cache.
/// </summary>
public sealed record AliasRuleEntry(
    long SiteId,
    string OldPath,
    string NewPath,
    int StatusCode = 301
);

/// <summary>
/// Composite key for site-scoped alias cache lookups.
/// </summary>
public readonly record struct SitePathKey(long SiteId, string Path);
