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

    public AliasRuleCacheWarmupService(IAliasRuleCache cache, ILogger<AliasRuleCacheWarmupService> log)
    {
        _cache = cache;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Warming alias rule cache...");
        await _cache.RefreshAsync(stoppingToken);
        _log.LogInformation("Alias rule cache warmup complete");
    }
}
