using System.Collections.Immutable;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Immutable-dictionary-backed alias cache keyed by composite <see cref="SitePathKey"/>.
/// Singleton. Lookups are O(1) with zero database or allocation overhead.
/// Site-scoped: same old path on two different sites is stored and retrieved independently.
/// </summary>
public sealed class AliasRuleCache : IAliasRuleCache
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AliasRuleCache> _log;
    private ImmutableDictionary<SitePathKey, AliasRuleEntry> _rules =
        ImmutableDictionary<SitePathKey, AliasRuleEntry>.Empty;

    public AliasRuleCache(IServiceProvider serviceProvider, ILogger<AliasRuleCache> log)
    {
        _serviceProvider = serviceProvider;
        _log = log;
    }

    public AliasRuleEntry? Find(long siteId, string oldPath)
    {
        var key = new SitePathKey(siteId, oldPath);
        _rules.TryGetValue(key, out var entry);
        return entry;
    }

    public void Invalidate()
    {
        _rules = ImmutableDictionary<SitePathKey, AliasRuleEntry>.Empty;
        _log.LogInformation("Alias rule cache invalidated");
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

            var aliases = await session.Query<AliasDocument>().ToListAsync(ct);
            var entries = aliases
                .Where(a => a.SiteId > 0 && !string.IsNullOrWhiteSpace(a.OldPath) && !string.IsNullOrWhiteSpace(a.NewPath))
                .Select(a => new AliasRuleEntry(
                    a.SiteId,
                    NormalizePath(a.OldPath),
                    a.NewPath))
                .ToList();

            _rules = entries.ToImmutableDictionary(
                e => new SitePathKey(e.SiteId, e.OldPath));

            _log.LogInformation("Alias rule cache refreshed with {Count} entries", entries.Count);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to refresh alias rule cache");
        }
    }

    private static string NormalizePath(string path)
        => (path.Trim().TrimEnd('/').ToLowerInvariant());
}
