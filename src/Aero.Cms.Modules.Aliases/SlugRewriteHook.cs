using Aero.Cms.Web.Core.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Registered page-save hook reserved for automatic slug-alias behavior. The
/// current implementation only logs execution and creates no alias or redirect.
/// </summary>
public class SlugRewriteHook(ILogger<SlugRewriteHook> logger) : IPageSaveHook
{
        /// <summary>
    /// Gets the hook ordering value. The current value is not a guarantee of
    /// persistence or redirect behavior because the hook is intentionally a no-op.
    /// </summary>
public int Order => -50; // Run early to capture original state if needed, or late? 
    // Usually late to ensure we only act on success? No, hooks run sequentially.
    // Order 0 is core save. So we should run after save to ensure it's persisted, 
    // or before to prepare the redirect.

        /// <summary>
    /// Logs the page-save operation and completes without inspecting, staging,
    /// committing, or publishing any alias changes.
    /// </summary>
public async Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct)
    {
        // Placeholder logic for detecting slug change
        // In a real implementation, we would compare ctx.Page current slug with DB version
        // if (ctx.Operation == "Publish" && slugChanged) { ... create RedirectRule ... }
        
        logger.LogInformation("SlugRewriteHook executed for operation {Operation}", ctx.Operation);
        await Task.CompletedTask;
    }
}
