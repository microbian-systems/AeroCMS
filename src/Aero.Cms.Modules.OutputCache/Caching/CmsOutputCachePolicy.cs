using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Aero.Cms.Modules.OutputCache.Caching;

/// <summary>
/// Custom output cache policy for CMS public pages.
///
/// Based on the default <see cref="OutputCachePolicy"/> but removes the
/// <c>Set-Cookie</c> check in <c>ServeResponseAsync</c>. This is necessary
/// because <c>UseAntiforgery()</c> adds an antiforgery token cookie to every
/// response, which would otherwise prevent the default policy from caching
/// any public page.
///
/// The antiforgery cookie is a security token, not user-specific data, so it
/// is safe to cache responses that contain it. Authenticated requests and
/// non-GET/HEAD methods are still excluded from caching.
/// </summary>
public sealed class CmsOutputCachePolicy : IOutputCachePolicy
{
        /// <summary>
    /// DiagnosticHeaderName.
    /// </summary>
public const string DiagnosticHeaderName = "X-Aero-Output-Cache";

        /// <summary>
    /// Instance.
    /// </summary>
public static readonly CmsOutputCachePolicy Instance = new();

        /// <summary>
    /// Initializes a new instance of the <see cref="CmsOutputCachePolicy"/> class.
    /// </summary>
public CmsOutputCachePolicy()
    {
    }

    /// <summary>
    /// Determines whether the current request should be eligible for output caching.
    /// Only GET/HEAD requests without authentication headers are cached.
    /// </summary>
    ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var attemptOutputCaching = AttemptOutputCaching(context);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;

        // The built-in output-cache policy includes the URL in its cache key.
        // This custom policy replaces that policy in order to allow the
        // antiforgery cookie, so it must restore the request-path partition
        // explicitly. Without it, a successful response for one public page
        // can be served for every page using this policy.
        context.CacheVaryByRules.VaryByValues["path"] =
            context.HttpContext.Request.PathBase.Add(context.HttpContext.Request.Path).Value ?? "/";
        context.CacheVaryByRules.VaryByValues["origin"] =
            $"{context.HttpContext.Request.Scheme}://{context.HttpContext.Request.Host.Value}".ToLowerInvariant();

        // Vary by all query parameters by default.
        context.CacheVaryByRules.QueryKeys = "*";
        context.CacheVaryByRules.VaryByValues["culture"] = CultureInfo.CurrentUICulture.Name;

        if (!attemptOutputCaching)
        {
            SetDiagnosticHeader(context, "BYPASS");
        }

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        SetDiagnosticHeader(context, "HIT");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called after the response is generated. Only caches HTTP 200 responses.
    /// Unlike the default policy, it does NOT exclude responses with Set-Cookie
    /// headers, since the antiforgery cookie is safe to cache alongside.
    ///
    /// Adds fine-grained per-page cache tags when the page model stored page
    /// context in <c>HttpContext.Items["AeroCms.PageContext"]</c>. This enables
    /// single-page eviction via <c>EvictByTagAsync("page-id-{id}")</c> without
    /// invalidating the entire <c>pages-list</c> tag.
    /// </summary>
    ValueTask IOutputCachePolicy.ServeResponseAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Only cache successful responses
        if (response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
            SetDiagnosticHeader(context, "BYPASS");
            return ValueTask.CompletedTask;
        }

        // Add per-page tags so individual pages can be evicted without
        // blowing away the entire pages-list cache. The page model stores
        // page ID and slug in HttpContext.Items during OnGetAsync under
        // "AeroCms.PageId" and "AeroCms.PageSlug" keys.
        AddPerPageTags(context);

        SetDiagnosticHeader(context, "MISS");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Extracts page ID and slug from HttpContext.Items and adds
    /// <c>page-id-{id}</c>, <c>page-slug-{slug}</c>, and
    /// <c>site-pages-{siteId}</c> tags to the OutputCache entry. These tags are then usable in
    /// <c>IOutputCacheStore.EvictByTagAsync</c> for single-page invalidation.
    ///
    /// Uses separate HttpContext.Items keys to avoid reflection:
    ///   "AeroCms.PageId"  → long (stored as boxed long)
    ///   "AeroCms.PageSlug" → string
    ///   "AeroCms.SiteId" → long (stored as boxed long)
    /// Both are set by DynamicPageModel.OnGetAsync after page load.
    /// </summary>
    private static void AddPerPageTags(OutputCacheContext context)
    {
        var items = context.HttpContext.Items;

        if (items["AeroCms.PageId"] is long id and > 0)
        {
            context.Tags.Add($"page-id-{id}");
        }

        if (items["AeroCms.PageSlug"] is string slug and { Length: > 0 })
        {
            var normalizedSlug = slug.ToLowerInvariant();
            context.Tags.Add($"page-slug-{normalizedSlug}");
            context.Tags.Add($"page-slug-{CultureInfo.CurrentUICulture.Name.ToLowerInvariant()}-{normalizedSlug}");
        }

        if (items["AeroCms.SiteId"] is long siteId and > 0)
        {
            context.Tags.Add($"site-pages-{siteId}");
        }
    }

    private static void SetDiagnosticHeader(OutputCacheContext context, string value)
    {
        var response = context.HttpContext.Response;
        if (response.HasStarted)
        {
            return;
        }

        response.Headers[DiagnosticHeaderName] = value;
    }

    private static bool AttemptOutputCaching(OutputCacheContext context)
    {
        var request = context.HttpContext.Request;

        // Only cache GET and HEAD requests
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        if (IsManagerOrAdminPath(request.Path))
        {
            return false;
        }

        // Do not cache authenticated requests
        if (!StringValues.IsNullOrEmpty(request.Headers.Authorization)
            || context.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return true;
    }

    private static bool IsManagerOrAdminPath(PathString path)
        => path.StartsWithSegments("/manager", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase);
}
