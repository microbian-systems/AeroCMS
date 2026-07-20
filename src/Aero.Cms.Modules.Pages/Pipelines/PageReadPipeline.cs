using Aero.Cms.Web.Core.Pipelines;

namespace Aero.Cms.Modules.Pages.Pipelines;

/// <summary>
/// Executes page-read hooks by ascending <see cref="IPageReadHook.Order"/>.
/// </summary>
/// <param name="hooks">The hooks available for each execution.</param>
/// <param name="logger">The pipeline logger.</param>
/// <remarks>
/// A hook can stop later hooks by short-circuiting the context. Exceptions from an
/// individual hook are logged and suppressed, so later hooks still run unless the
/// context was short-circuited.
/// </remarks>
public class PageReadPipeline(IEnumerable<IPageReadHook> hooks, ILogger<PageReadPipeline> logger)
{
    /// <summary>
    /// Executes all page read hooks in order, short-circuiting if any hook calls ShortCircuit().
    /// </summary>
    /// <param name="ctx">The page read context.</param>
    /// <param name="ct">The token passed to each hook.</param>
    /// <returns>A task that completes after the pipeline stops or all hooks have run.</returns>
    public async Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var orderedHooks = hooks.OrderBy(h => h.Order).ToList();

        logger.LogDebug("Executing PageReadPipeline with {HookCount} hooks", orderedHooks.Count);

        foreach (var hook in orderedHooks)
        {
            if (ctx.IsShortCircuited)
            {
                logger.LogDebug("Pipeline short-circuited at hook {HookType}, reason: {Reason}",
                    hook.GetType().Name, ctx.ShortCircuitReason);
                break;
            }

            try
            {
                logger.LogDebug("Executing hook {HookType} (Order: {Order})",
                    hook.GetType().Name, hook.Order);

                await hook.ExecuteAsync(ctx, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Hook {HookType} threw an exception. Continuing to next hook.",
                    hook.GetType().Name);
            }
        }

        logger.LogDebug("PageReadPipeline execution completed. Short-circuited: {IsShortCircuited}",
            ctx.IsShortCircuited);
    }
}
