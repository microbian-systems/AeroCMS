using Aero.Cms.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Aero.Cms.Modules.OutputCache.Caching;

/// <summary>
/// Applies Aero CMS eligibility, cache-key variation, diagnostics, and resource tags to
/// ASP.NET Core output-cache entries.
/// </summary>
/// <remarks>
/// <para>
/// The policy permits cache lookup and storage only for anonymous <c>GET</c> and <c>HEAD</c>
/// requests outside <c>/manager</c>, <c>/admin</c>, and <c>/api/v1/admin</c>. A response is
/// retained only when it has status code 200 and does not contain a <c>Set-Cookie</c> header.
/// Resource locking remains enabled.
/// </para>
/// <para>
/// Cache keys are partitioned by scheme and host, path base and path, current UI culture,
/// the resolved site and exact persisted theme selection, and, by default, every query parameter.
/// A named policy can replace the query-key set after this policy runs. Request cookies and
/// arbitrary request headers are not key dimensions. Consequently, endpoints using this policy
/// must produce output that is safe to share among anonymous requests with the same configured
/// dimensions; this policy is not a cookie or user-isolation boundary.
/// </para>
/// <para>
/// This type configures the ASP.NET Core response-output cache. It neither reads nor writes
/// FusionCache entries and does not perform invalidation itself. Callers invalidate entries
/// through <see cref="IOutputCacheStore.EvictByTagAsync(string, CancellationToken)"/> using
/// the coarse policy tags or the resource tags added after a cacheable response is produced.
/// </para>
/// <para>
/// Instances contain no mutable instance state. The policy callbacks complete synchronously,
/// ignore their cancellation token, and mutate only the supplied <see cref="OutputCacheContext"/>.
/// </para>
/// </remarks>
public sealed class CmsOutputCachePolicy : IOutputCachePolicy
{
    /// <summary>
    /// The response-header name used to report this policy's cache decision.
    /// </summary>
    /// <remarks>
    /// The policy writes <c>HIT</c> when a cached response is served, <c>MISS</c> after an
    /// eligible response is produced, and <c>BYPASS</c> when request or response checks reject
    /// caching. <c>MISS</c> describes this policy's decision and does not guarantee that another
    /// policy or the output-cache store ultimately retained the response.
    /// </remarks>
    public const string DiagnosticHeaderName = "X-Aero-Output-Cache";

    /// <summary>
    /// Gets a reusable stateless policy instance for registrations that accept an
    /// <see cref="IOutputCachePolicy"/> instance.
    /// </summary>
    /// <remarks>
    /// <see cref="OutputCacheModule"/> registers the policy by type so ASP.NET Core creates it
    /// through dependency injection; this field supports direct instance-based registrations.
    /// </remarks>
    public static readonly CmsOutputCachePolicy Instance = new();

    /// <summary>
    /// Initializes a stateless output-cache policy.
    /// </summary>
    public CmsOutputCachePolicy()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Enables output caching and locking, evaluates request eligibility, and establishes
    /// origin, path, query, UI-culture, and resolved site/theme variation. Ineligible requests receive the
    /// <c>BYPASS</c> diagnostic value when the response has not started.
    /// </remarks>
    ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var attemptOutputCaching = AttemptOutputCaching(context);
        var allowCacheLookup = attemptOutputCaching
            && !RequestRequiresRevalidation(context.HttpContext.Request);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = allowCacheLookup;
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

        if (context.HttpContext.Features.Get<IAeroSiteSlice>() is { } site)
        {
            context.CacheVaryByRules.VaryByValues["site-id"] =
                site.SiteId.ToString(CultureInfo.InvariantCulture);
            context.CacheVaryByRules.VaryByValues["theme-id"] = site.ThemeId;
            context.CacheVaryByRules.VaryByValues["theme-version"] = site.ThemeVersion;
            context.CacheVaryByRules.VaryByValues["theme-revision"] =
                site.ThemeRevision.ToString(CultureInfo.InvariantCulture);
        }

        if (!attemptOutputCaching)
        {
            SetDiagnosticHeader(context, "BYPASS");
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>HIT</c> diagnostic value when the response has not started.
    /// </remarks>
    ValueTask IOutputCachePolicy.ServeFromCacheAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        SetDiagnosticHeader(context, "HIT");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Rejects non-200 responses and responses containing <c>Set-Cookie</c>. For an accepted
    /// response, adds any resource tags represented by supported <see cref="HttpContext.Items"/>
    /// values and writes the <c>MISS</c> diagnostic value.
    /// </remarks>
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

        // Never store or replay cookies from a shared output-cache entry.
        // This covers authentication, consent, culture, session, experiments,
        // antiforgery, and any cookies introduced by future middleware.
        if (response.Headers.ContainsKey("Set-Cookie"))
        {
            context.AllowCacheStorage = false;
            SetDiagnosticHeader(context, "BYPASS");
            return ValueTask.CompletedTask;
        }

        // Add per-page tags so individual pages can be evicted without
        // blowing away the entire pages-list cache. The page model stores
        // page ID and slug in HttpContext.Items during OnGetAsync under
        // "AeroCms.PageId" and "AeroCms.PageSlug" keys.
        AddResourceTags(context);

        SetDiagnosticHeader(context, "MISS");
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Adds output-cache eviction tags from resource metadata stored in
    /// <see cref="HttpContext.Items"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A positive <c>AeroCms.PageId</c> produces <c>page-id-{id}</c>. A non-empty
    /// <c>AeroCms.PageSlug</c> produces both <c>page-slug-{slug}</c> and
    /// <c>page-slug-{culture}-{slug}</c>, using invariant lower-case values. When either page
    /// value is present and <c>AeroCms.SiteId</c> is a positive <see cref="long"/>,
    /// <c>site-pages-{siteId}</c> is also added. The ID and unqualified slug tags are not
    /// site-scoped.
    /// </para>
    /// <para>
    /// When a positive site ID is accompanied by a non-blank <c>AeroCms.ContentTypeAlias</c>,
    /// the method adds <c>content-public:{siteId}</c> and
    /// <c>content-type:{siteId}:{typeAlias}</c>. A positive <c>AeroCms.ContentItemId</c> adds
    /// <c>content-item:{siteId}:{itemId}</c>. Non-blank <c>AeroCms.ContentItemSlug</c> and
    /// <c>AeroCms.ContentCulture</c> values together add
    /// <c>content-item-slug:{siteId}:{typeAlias}:{culture}:{slug}</c>.
    /// A string collection in <c>AeroCms.ContentTypeAliases</c> adds the public and
    /// type-specific tags for every content type used by a composed page.
    /// </para>
    /// Missing, blank, non-positive, or differently typed item values are ignored. Tags make
    /// entries addressable by <see cref="IOutputCacheStore.EvictByTagAsync(string, CancellationToken)"/>;
    /// adding them does not itself evict any cache entry.
    /// </remarks>
    private static void AddResourceTags(OutputCacheContext context)
    {
        var items = context.HttpContext.Items;
        var hasPageMetadata = false;

        if (items["AeroCms.PageId"] is long id and > 0)
        {
            context.Tags.Add($"page-id-{id}");
            hasPageMetadata = true;
        }

        if (items["AeroCms.PageSlug"] is string slug and { Length: > 0 })
        {
            var normalizedSlug = slug.ToLowerInvariant();
            context.Tags.Add($"page-slug-{normalizedSlug}");
            context.Tags.Add($"page-slug-{CultureInfo.CurrentUICulture.Name.ToLowerInvariant()}-{normalizedSlug}");
            hasPageMetadata = true;
        }

        if (items["AeroCms.SiteId"] is long siteId and > 0)
        {
            if (hasPageMetadata)
            {
                context.Tags.Add($"site-pages-{siteId}");
            }

            var contentTypeAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (items["AeroCms.ContentTypeAliases"] is IEnumerable<string> composedAliases)
            {
                foreach (var alias in composedAliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
                {
                    contentTypeAliases.Add(alias.Trim());
                }
            }

            if (items["AeroCms.ContentTypeAlias"] is string contentTypeAlias
                && !string.IsNullOrWhiteSpace(contentTypeAlias))
            {
                contentTypeAliases.Add(contentTypeAlias.Trim());
            }

            if (contentTypeAliases.Count > 0)
            {
                context.Tags.Add($"content-public:{siteId}");
                foreach (var alias in contentTypeAliases)
                {
                    context.Tags.Add($"content-type:{siteId}:{alias.ToLowerInvariant()}");
                }
            }

            if (items["AeroCms.ContentTypeAlias"] is string itemTypeAlias
                && !string.IsNullOrWhiteSpace(itemTypeAlias))
            {
                var normalizedType = itemTypeAlias.Trim().ToLowerInvariant();
                if (items["AeroCms.ContentItemId"] is long contentItemId and > 0)
                {
                    context.Tags.Add($"content-item:{siteId}:{contentItemId}");
                }

                if (items["AeroCms.ContentItemSlug"] is string contentSlug &&
                    items["AeroCms.ContentCulture"] is string contentCulture &&
                    !string.IsNullOrWhiteSpace(contentSlug) &&
                    !string.IsNullOrWhiteSpace(contentCulture))
                {
                    context.Tags.Add(
                        $"content-item-slug:{siteId}:{normalizedType}:" +
                        $"{contentCulture.Trim().ToLowerInvariant()}:" +
                        $"{contentSlug.Trim().Trim('/').ToLowerInvariant()}");
                }
            }
        }
    }

    /// <summary>
    /// Writes a diagnostic value unless the HTTP response has already started.
    /// </summary>
    private static void SetDiagnosticHeader(OutputCacheContext context, string value)
    {
        var response = context.HttpContext.Response;
        if (response.HasStarted)
        {
            return;
        }

        response.Headers[DiagnosticHeaderName] = value;
    }

    /// <summary>
    /// Determines whether a request is eligible for cache lookup and storage.
    /// </summary>
    /// <remarks>
    /// Eligibility requires <c>GET</c> or <c>HEAD</c>, a path outside the manager and admin
    /// prefixes, no <c>Authorization</c> header, and no authenticated principal. Endpoint
    /// authorization metadata and request cookies are not inspected.
    /// </remarks>
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

    /// <summary>
    /// Determines whether an otherwise eligible request requires origin revalidation.
    /// </summary>
    /// <remarks>
    /// Directive names are matched as complete comma-separated tokens. A
    /// syntactically valid <c>max-age</c> delta-seconds value is treated numerically,
    /// so both <c>0</c> and values containing only leading zeroes require revalidation.
    /// </remarks>
    private static bool RequestRequiresRevalidation(HttpRequest request)
        => HasDirective(request.Headers.CacheControl, "no-cache")
           || HasZeroDeltaSecondsDirective(request.Headers.CacheControl, "max-age")
           || HasDirective(request.Headers.Pragma, "no-cache");

    /// <summary>
    /// Finds one exact directive in one or more comma-separated header values.
    /// </summary>
    private static bool HasDirective(
        StringValues headerValues,
        string expectedName)
    {
        foreach (var headerValue in headerValues)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            foreach (var candidate in headerValue.Split(','))
            {
                var separatorIndex = candidate.IndexOf('=');
                var name = (separatorIndex >= 0
                        ? candidate.AsSpan(0, separatorIndex)
                        : candidate.AsSpan())
                    .Trim();

                if (!name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds an exact directive whose value is a syntactically valid zero delta-seconds.
    /// </summary>
    private static bool HasZeroDeltaSecondsDirective(
        StringValues headerValues,
        string expectedName)
    {
        foreach (var headerValue in headerValues)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            foreach (var candidate in headerValue.Split(','))
            {
                var separatorIndex = candidate.IndexOf('=');
                if (separatorIndex < 0
                    || !candidate.AsSpan(0, separatorIndex).Trim()
                        .Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = candidate.AsSpan(separatorIndex + 1).Trim();
                if (!value.IsEmpty && value.IndexOfAnyExcept('0') < 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a path belongs to a manager or admin route prefix.
    /// </summary>
    private static bool IsManagerOrAdminPath(PathString path)
        => path.StartsWithSegments("/manager", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase);
}
