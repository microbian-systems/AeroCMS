using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

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
    public const string DiagnosticHeaderName = "X-Aero-Output-Cache";

    public static readonly CmsOutputCachePolicy Instance = new();

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

        // Vary by all query parameters by default
        context.CacheVaryByRules.QueryKeys = "*";
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

        SetDiagnosticHeader(context, "MISS");
        return ValueTask.CompletedTask;
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

        // Do not cache authenticated requests
        if (!StringValues.IsNullOrEmpty(request.Headers.Authorization)
            || context.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return true;
    }
}
