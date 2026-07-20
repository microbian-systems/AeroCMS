using Aero.Cms.Web.Core.Pipelines;

namespace Aero.Cms.Modules.Pages.Pipelines.Hooks;

/// <summary>
/// Short-circuits non-public pages when the current principal is unauthenticated.
/// </summary>
/// <param name="httpContextAccessor">Provides the current HTTP principal.</param>
/// <param name="logger">The hook logger.</param>
/// <remarks>
/// A missing HTTP context or page document causes the hook to allow the pipeline to
/// continue. This hook checks authentication only; it does not evaluate roles,
/// policies, or resource-specific authorization.
/// </remarks>
public class AuthorizationHook(IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationHook> logger)
    : IPageReadHook
{
    /// <summary>
    /// Order 0 - runs first to gate access before any other processing.
    /// </summary>
    public int Order => 0;

    /// <summary>
    /// Applies the authentication gate to the page currently stored in the context.
    /// </summary>
    /// <param name="ctx">The mutable page-read context.</param>
    /// <param name="ct">Unused; the operation performs no asynchronous work.</param>
    /// <returns>A completed task.</returns>
public Task ExecuteAsync(PageReadContext ctx, CancellationToken ct)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            logger.LogWarning("AuthorizationHook: HttpContext is null, skipping authorization check");
            return Task.CompletedTask;
        }

        // If page is already short-circuited (e.g., cache hit), skip authorization check
        if (ctx.IsShortCircuited)
        {
            return Task.CompletedTask;
        }

        // Get the page from context if available
        var page = ctx.Page as PageDocument;
        if (page == null)
        {
            // No page loaded yet, skip authorization check (will be handled by subsequent hooks or page loading)
            return Task.CompletedTask;
        }

        // Check if page requires authorization
        if (!page.IsPubliclyVisible)
        {
            var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated ?? false;

            if (!isAuthenticated)
            {
                logger.LogInformation(
                    "AuthorizationHook: Page '{Slug}' requires authorization but user is not authenticated. Short-circuiting.",
                    page.Slug);

                ctx.ShortCircuit("Unauthorized");
                return Task.CompletedTask;
            }

            logger.LogDebug("AuthorizationHook: Page '{Slug}' requires authorization and user is authenticated",
                page.Slug);
        }
        else
        {
            logger.LogDebug("AuthorizationHook: Page '{Slug}' is publicly visible, allowing access",
                page.Slug);
        }

        return Task.CompletedTask;
    }
}
