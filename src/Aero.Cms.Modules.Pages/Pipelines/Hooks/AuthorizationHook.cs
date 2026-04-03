using Aero.Cms.Core.Entities;
using Aero.Cms.Web.Core.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Pages.Pipelines.Hooks;

/// <summary>
/// Hook that checks if the page requires authorization and short-circuits if user is not authenticated.
/// </summary>
public class AuthorizationHook(IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationHook> logger)
    : IPageReadHook
{
    /// <summary>
    /// Order 0 - runs first to gate access before any other processing.
    /// </summary>
    public int Order => 0;

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
