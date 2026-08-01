using Aero.Cms.Modules.Aliases.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Aliases.Handlers;

/// <summary>
/// Wolverine handler that clears the alias snapshot and then attempts a full
/// refresh after a committed alias mutation. Clearing occurs first so a refresh
/// failure leaves an empty snapshot rather than serving a known-stale redirect;
/// request-time fallback may still resolve aliases from persistence.
/// </summary>
[WolverineHandler]
public sealed class AliasCacheInvalidationHandler(
    IAliasRuleCache cache,
    ILogger<AliasCacheInvalidationHandler> log) : IWolverineHandler
{
    /// <summary>Invalidates and refreshes the cache for a created alias.</summary>
public async Task Handle(AliasCreated e)
    {
        log.LogInformation("Alias created ({Id}, '{OldPath}' → '{NewPath}') — refreshing cache",
            e.Document.Id, e.Document.OldPath, e.Document.NewPath);
        await InvalidateAndRefreshAsync();
    }

    /// <summary>Invalidates and refreshes the cache for an updated alias.</summary>
public async Task Handle(AliasUpdated e)
    {
        log.LogInformation("Alias updated ({Id}, '{OldPath}' → '{NewPath}') — refreshing cache",
            e.Document.Id, e.Document.OldPath, e.Document.NewPath);
        await InvalidateAndRefreshAsync();
    }

    /// <summary>Invalidates and refreshes the cache for a deleted alias.</summary>
public async Task Handle(AliasDeleted e)
    {
        log.LogInformation("Alias deleted ({Id}) — refreshing cache", e.Document.Id);
        await InvalidateAndRefreshAsync();
    }

    private async Task InvalidateAndRefreshAsync()
    {
        // Empty-cache fallback is safer than serving a stale permanent redirect
        // if the database refresh fails.
        cache.Invalidate();
        await cache.RefreshAsync();
    }
}
