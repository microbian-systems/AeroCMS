using System.Collections.Immutable;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Process-wide <see cref="IAliasRuleCache"/> implementation backed by an immutable
/// dictionary. Each successful refresh constructs a complete replacement snapshot
/// and publishes it atomically; readers therefore observe either the preceding
/// snapshot or the complete replacement, never a partially populated dictionary.
/// Failed refreshes are logged and retain the existing snapshot. Duplicate
/// normalized keys cause snapshot construction to fail, with the existing
/// snapshot likewise retained.
/// </summary>
public sealed class AliasRuleCache : IAliasRuleCache
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AliasRuleCache> _log;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private ImmutableDictionary<SitePathKey, AliasRuleEntry> _rules =
        ImmutableDictionary<SitePathKey, AliasRuleEntry>.Empty;

    /// <summary>Initializes the cache with an empty snapshot.</summary>
public AliasRuleCache(IServiceProvider serviceProvider, ILogger<AliasRuleCache> log)
    {
        _serviceProvider = serviceProvider;
        _log = log;
    }

    /// <inheritdoc />
public AliasRuleEntry? Find(long siteId, string culture, string oldPath)
    {
        var key = new SitePathKey(
            siteId,
            AliasDocument.NormalizeCulture(culture),
            AliasDocument.NormalizePath(oldPath));
        _rules.TryGetValue(key, out var entry);
        return entry;
    }

    /// <inheritdoc />
public void Invalidate()
    {
        _rules = ImmutableDictionary<SitePathKey, AliasRuleEntry>.Empty;
        _log.LogInformation("Alias rule cache invalidated");
    }

    /// <inheritdoc />
public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _refreshGate.WaitAsync(ct);
        try
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();

                var aliases = await session.Query<AliasDocument>().ToListAsync(ct);
                var entries = aliases
                    .Where(a => a.SiteId > 0 && !string.IsNullOrWhiteSpace(a.OldPath) && !string.IsNullOrWhiteSpace(a.NewPath))
                    .Select(a => new AliasRuleEntry(
                        a.SiteId,
                        AliasDocument.NormalizeCulture(a.Culture),
                        AliasDocument.NormalizePath(a.OldPath),
                        AliasDocument.NormalizePath(a.NewPath),
                        a.StatusCode))
                    .ToList();

                _rules = entries.ToImmutableDictionary(
                    e => new SitePathKey(e.SiteId, e.Culture, e.OldPath));

                _log.LogInformation("Alias rule cache refreshed with {Count} entries", entries.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to refresh alias rule cache");
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

}
