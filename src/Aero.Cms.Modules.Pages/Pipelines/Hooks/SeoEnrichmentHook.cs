using Aero.Cms.Web.Core.Pipelines;

namespace Aero.Cms.Modules.Pages.Pipelines.Hooks;

/// <summary>
/// Adds page-derived SEO and page-kind values to read-pipeline metadata.
/// </summary>
/// <param name="logger">The hook logger.</param>
/// <remarks>
/// Existing <c>SeoTitle</c> and <c>PageKind</c> values are overwritten. An empty SEO
/// description leaves any existing <c>SeoDescription</c> value unchanged.
/// </remarks>
public class SeoEnrichmentHook(ILogger<SeoEnrichmentHook> logger) : IPageReadHook
{
    /// <summary>
    /// Order 50 - runs after authorization hook to enrich the response with SEO data.
    /// </summary>
    public int Order => 50;

    /// <summary>
    /// Enriches metadata when a page is present and the context has not short-circuited.
    /// </summary>
    /// <param name="ctx">The mutable page-read context.</param>
    /// <param name="ct">Unused; the operation performs no asynchronous work.</param>
    /// <returns>A completed task.</returns>
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
