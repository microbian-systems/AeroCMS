using Aero.Cms.Web.Core.Pipelines;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages.Pipelines.Hooks;

/// <summary>
/// Hook that enriches the context with SEO metadata extracted from the page document.
/// </summary>
public class SeoEnrichmentHook(ILogger<SeoEnrichmentHook> logger) : IPageReadHook
{
    /// <summary>
    /// Order 50 - runs after authorization hook to enrich the response with SEO data.
    /// </summary>
    public int Order => 50;

    public Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        // Skip if context is already short-circuited
        if (ctx.IsShortCircuited)
        {
            logger.LogDebug("SeoEnrichmentHook: Context is short-circuited, skipping SEO enrichment");
            return Task.CompletedTask;
        }

        // Get the page from context
        var page = ctx.Page as PageDocument;
        if (page == null)
        {
            logger.LogDebug("SeoEnrichmentHook: No page document available, skipping SEO enrichment");
            return Task.CompletedTask;
        }

        // Extract and add SEO title
        var seoTitle = page.SeoTitle ?? page.Title;
        ctx.Metadata["SeoTitle"] = seoTitle;

        logger.LogDebug("SeoEnrichmentHook: Added SeoTitle '{SeoTitle}' to metadata", seoTitle);

        // Extract and add SEO description
        if (!string.IsNullOrEmpty(page.SeoDescription))
        {
            ctx.Metadata["SeoDescription"] = page.SeoDescription;
            logger.LogDebug("SeoEnrichmentHook: Added SeoDescription to metadata");
        }

        // Add page kind for additional context
        ctx.Metadata["PageKind"] = page.Kind;

        return Task.CompletedTask;
    }
}
