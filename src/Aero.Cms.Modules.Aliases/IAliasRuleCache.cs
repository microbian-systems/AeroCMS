using System.Collections.Immutable;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Hot in-memory alias lookup. Zero database I/O per request.
/// Cache is refreshed once on startup via <see cref="AliasRuleCacheWarmupService"/>
/// and invalidated when aliases are created, updated, or deleted.
/// </summary>
public interface IAliasRuleCache
{
    /// <summary>
    /// Finds an alias rule matching the normalized old path.
    /// Returns null if no match. O(1) dictionary lookup.
    /// </summary>
    AliasRuleEntry? Find(string oldPath);

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
