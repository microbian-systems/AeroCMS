using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// <see cref="BackgroundService"/> that eagerly loads the alias rule cache
/// from the database on application startup.
/// </summary>
public sealed class AliasRuleCacheWarmupService : BackgroundService
{
    private readonly IAliasRuleCache _cache;
    private readonly ILogger<AliasRuleCacheWarmupService> _log;

        /// <summary>
    /// Initializes a new instance of the <see cref="AliasRuleCacheWarmupService"/> class.
    /// </summary>
public AliasRuleCacheWarmupService(IAliasRuleCache cache, ILogger<AliasRuleCacheWarmupService> log)
    {
        _cache = cache;
        _log = log;
    }

        /// <summary>
    /// ExecuteAsync method.
    /// </summary>
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Warming alias rule cache...");
        await _cache.RefreshAsync(stoppingToken);
        _log.LogInformation("Alias rule cache warmup complete");
    }
}
