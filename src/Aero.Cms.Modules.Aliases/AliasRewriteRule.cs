using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using Aero.Cms.Shared.Localization;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Rewrite rule that turns a matching site-scoped alias into a terminal redirect.
/// It requires an <see cref="IAeroSiteSlice"/> feature with a positive site ID;
/// without one it passes the request through unchanged. Hosts must place site
/// resolution before this rule for alias resolution to work, but this type does
/// not itself guarantee that middleware ordering.
/// <para>
/// The rule resolves the request culture from the site's supported cultures,
/// removes a recognized culture prefix for lookup, and normalizes the remaining
/// path. It first reads <see cref="IAliasRuleCache"/> and, on a miss, synchronously
/// queries a scoped document session. A matching prefixed request keeps that
/// culture prefix in its <c>Location</c>; the query string is also preserved.
/// </para>
/// </summary>
public sealed class AliasRewriteRule : IRule
{
    private readonly IAliasRuleCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AliasRewriteRule> _log;

    /// <summary>Initializes the singleton rewrite rule and its fallback dependencies.</summary>
public AliasRewriteRule(IAliasRuleCache cache, IServiceProvider serviceProvider, ILogger<AliasRewriteRule> log)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
        _log = log;
    }

    /// <summary>
    /// Applies a redirect for a resolved alias, otherwise leaves the rewrite
    /// context unchanged. The cache-miss persistence query is synchronous because
    /// <see cref="IRule"/> is synchronous; unavailable document-session services
    /// and unmatched aliases pass through rather than producing a response.
    /// </summary>
public void ApplyRule(RewriteContext context)
    {
        var http = context.HttpContext;
        var rawPath = http.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(rawPath)) return;

        // Resolve current site from features (set by SiteResolutionMiddleware)
        var slice = http.Features.Get<IAeroSiteSlice>();
        if (slice is null || slice.SiteId <= 0) return; // no site resolved — skip aliases

        // Alias rewriting runs before UseRequestLocalization in the production
        // pipeline. Resolve the site-supported culture directly instead of
        // depending on culture items that may not have been populated yet.
        var culture = AeroCultureRoute.ResolveRequestCulture(
            http.Request.Path,
            slice.DefaultCulture,
            slice.SupportedCultures,
            out var culturePrefix);
        var path = NormalizePath(RemoveCulturePrefix(rawPath, culturePrefix));

        // Fast path — check in-memory cache (site-scoped)
        var entry = _cache.Find(slice.SiteId, culture, path);
        if (entry is not null)
        {
            ApplyEntry(http, entry, culturePrefix is not null, context);
            return;
        }

        // Cache miss — fall back to DB query (site-scoped)
        _log.LogDebug("Cache miss for SiteId={SiteId} Path='{Path}' — querying AeroDB", slice.SiteId, path);

        var scope = _serviceProvider.CreateAsyncScope();
        try
        {
            var session = scope.ServiceProvider.GetService<IDocumentSession>();
            if (session is null)
            {
                _log.LogDebug(
                    "No document session is available for alias cache-miss fallback; passing through SiteId={SiteId} Path='{Path}'",
                    slice.SiteId,
                    path);
                return;
            }

            var aliases = session.Query<AliasDocument>()
                .Where(x => x.SiteId == slice.SiteId && x.Culture == culture)
                .ToList();

            foreach (var alias in aliases)
            {
                var aliasPath = NormalizePath(alias.OldPath);
                if (string.Equals(aliasPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    _log.LogDebug("DB fallback matched '{OldPath}' → '{NewPath}' for SiteId={SiteId} Path='{Path}'",
                        alias.OldPath, alias.NewPath, slice.SiteId, path);

                    ApplyEntry(http, new AliasRuleEntry(
                        alias.SiteId,
                        alias.Culture,
                        aliasPath,
                        alias.NewPath,
                        alias.StatusCode),
                        culturePrefix is not null,
                        context);
                    return;
                }
            }
        }
        finally
        {
            scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        // No match in cache or DB
        _log.LogDebug("No alias found for SiteId={SiteId} Path='{Path}'", slice.SiteId, path);
    }

    private static void ApplyEntry(
        HttpContext http,
        AliasRuleEntry entry,
        bool preserveCulturePrefix,
        RewriteContext context)
    {
        http.Response.StatusCode = entry.StatusCode;
        http.Response.Headers[HeaderNames.Location] =
            (preserveCulturePrefix
                ? AeroCultureRoute.BuildCulturePath(entry.Culture, entry.NewPath)
                : entry.NewPath)
            + http.Request.QueryString;
        context.Result = RuleResult.EndResponse;
    }

    private static string NormalizePath(string? path)
        => AliasDocument.NormalizePath(path);

    private static string RemoveCulturePrefix(string path, string? culturePrefix)
    {
        if (string.IsNullOrWhiteSpace(culturePrefix))
            return path;

        var prefix = "/" + culturePrefix.Trim('/');
        if (string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase))
            return "/";

        return path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }
}
