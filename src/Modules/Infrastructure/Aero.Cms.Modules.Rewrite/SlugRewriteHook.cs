using Aero.Cms.Web.Core.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Rewrite;

public class SlugRewriteHook(ILogger<SlugRewriteHook> logger) : IPageSaveHook
{
    public int Order => -50; // Run early to capture original state if needed, or late? 
    // Usually late to ensure we only act on success? No, hooks run sequentially.
    // Order 0 is core save. So we should run after save to ensure it's persisted, 
    // or before to prepare the redirect.

    public async Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct)
    {
        // Placeholder logic for detecting slug change
        // In a real implementation, we would compare ctx.Page current slug with DB version
        // if (ctx.Operation == "Publish" && slugChanged) { ... create RedirectRule ... }
        
        logger.LogInformation("SlugRewriteHook executed for operation {Operation}", ctx.Operation);
        await Task.CompletedTask;
    }
}

public record RedirectRule
{
    public long Id { get; init; }
    public required string FromPath { get; init; }
    public required string ToPath { get; init; }
    public int StatusCode { get; init; } = 301;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
