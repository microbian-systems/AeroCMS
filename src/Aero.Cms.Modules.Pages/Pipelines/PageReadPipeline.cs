using Aero.Cms.Core.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages.Pipelines;

/// <summary>
/// Pipeline runner that executes IPageReadHook implementations in order.
/// </summary>
public class PageReadPipeline
{
    private readonly IEnumerable<IPageReadHook> _hooks;
    private readonly ILogger<PageReadPipeline> _logger;

    public PageReadPipeline(IEnumerable<IPageReadHook> hooks, ILogger<PageReadPipeline> logger)
    {
        _hooks = hooks;
        _logger = logger;
    }

    /// <summary>
    /// Executes all page read hooks in order, short-circuiting if any hook calls ShortCircuit().
    /// </summary>
    /// <param name="ctx">The page read context.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var orderedHooks = _hooks.OrderBy(h => h.Order).ToList();

        _logger.LogDebug("Executing PageReadPipeline with {HookCount} hooks", orderedHooks.Count);

        foreach (var hook in orderedHooks)
        {
            if (ctx.IsShortCircuited)
            {
                _logger.LogDebug("Pipeline short-circuited at hook {HookType}, reason: {Reason}",
                    hook.GetType().Name, ctx.ShortCircuitReason);
                break;
            }

            try
            {
                _logger.LogDebug("Executing hook {HookType} (Order: {Order})",
                    hook.GetType().Name, hook.Order);

                await hook.ExecuteAsync(ctx, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hook {HookType} threw an exception. Continuing to next hook.",
                    hook.GetType().Name);
            }
        }

        _logger.LogDebug("PageReadPipeline execution completed. Short-circuited: {IsShortCircuited}",
            ctx.IsShortCircuited);
    }
}
