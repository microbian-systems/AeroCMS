using Aero.Cms.Modules.Aliases.Events;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Attributes;

namespace Aero.Cms.Modules.Aliases.Handlers;

/// <summary>
/// Wolverine message handler that refreshes the alias rule cache
/// whenever an alias is created, updated, or deleted.
///
/// <see cref="IAliasRuleCache.RefreshAsync"/> reloads all aliases
/// from AeroDB into the immutable dictionary.
/// </summary>
[WolverineHandler]
public sealed class AliasCacheInvalidationHandler(
    IAliasRuleCache cache,
    ILogger<AliasCacheInvalidationHandler> log) : IWolverineHandler
{
        /// <summary>
    /// Handle method.
    /// </summary>
public async Task Handle(AliasCreated e)
    {
        log.LogInformation("Alias created ({Id}, '{OldPath}' → '{NewPath}') — refreshing cache",
            e.Document.Id, e.Document.OldPath, e.Document.NewPath);
        await InvalidateAndRefreshAsync();
    }

        /// <summary>
    /// Handle method.
    /// </summary>
public async Task Handle(AliasUpdated e)
    {
        log.LogInformation("Alias updated ({Id}, '{OldPath}' → '{NewPath}') — refreshing cache",
            e.Document.Id, e.Document.OldPath, e.Document.NewPath);
        await InvalidateAndRefreshAsync();
    }

        /// <summary>
    /// Handle method.
    /// </summary>
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
