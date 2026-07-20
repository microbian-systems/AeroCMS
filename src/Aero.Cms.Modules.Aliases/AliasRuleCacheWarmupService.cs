using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Hosted service that attempts one alias-cache refresh during application
/// startup. It does not retry. Cancellation while the cache is waiting for its
/// refresh gate propagates from this service; cancellation or another failure
/// during persistence loading is caught and logged by the cache. Request-time
/// alias resolution can still use its persistence fallback when a document
/// session is available.
/// </summary>
public sealed class AliasRuleCacheWarmupService : BackgroundService
{
    private readonly IAliasRuleCache _cache;
    private readonly ILogger<AliasRuleCacheWarmupService> _log;

    /// <summary>Initializes the hosted cache warmup service.</summary>
public AliasRuleCacheWarmupService(IAliasRuleCache cache, ILogger<AliasRuleCacheWarmupService> log)
    {
        _cache = cache;
        _log = log;
    }

    /// <summary>
    /// Requests one cache refresh with the host shutdown token. See
    /// <see cref="IAliasRuleCache.RefreshAsync(CancellationToken)"/> for the
    /// distinction between cancellation before and after gate acquisition.
    /// </summary>
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Warming alias rule cache...");
        await _cache.RefreshAsync(stoppingToken);
        _log.LogInformation("Alias rule cache warmup complete");
    }
}
