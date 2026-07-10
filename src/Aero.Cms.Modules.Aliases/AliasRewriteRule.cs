using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Custom <see cref="IRule"/> that evaluates URL aliases scoped to the current site.
///
/// Resolves the current site from <see cref="IAeroSiteSlice"/> set by
/// <see cref="SiteResolutionMiddleware"/>. Only aliases belonging to the current
/// site are checked — two sites can have the same old path resolving to different
/// new paths.
///
/// Primary path: reads from the in-memory <see cref="IAliasRuleCache"/> (zero DB I/O).
/// Cache-miss fallback: queries AeroDB directly, scoped to current site.
///
/// Warmup: <see cref="AliasRuleCacheWarmupService"/> loads the cache from the DB on startup.
/// Invalidation: cache is invalidated on create/update/delete via <see cref="IAliasRuleCache.Invalidate"/>.
///
/// Registered as a singleton and added to <see cref="RewriteOptions"/>
/// via the <see cref="AliasStartupFilter"/>.
/// </summary>
public sealed class AliasRewriteRule : IRule
{
    private readonly IAliasRuleCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AliasRewriteRule> _log;

    public AliasRewriteRule(IAliasRuleCache cache, IServiceProvider serviceProvider, ILogger<AliasRewriteRule> log)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
        _log = log;
    }

    public void ApplyRule(RewriteContext context)
    {
        var http = context.HttpContext;
        var rawPath = http.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(rawPath)) return;

        // Resolve current site from features (set by SiteResolutionMiddleware)
        var slice = http.Features.Get<IAeroSiteSlice>();
        if (slice is null || slice.SiteId <= 0) return; // no site resolved — skip aliases

        var path = NormalizePath(rawPath);

        // Fast path — check in-memory cache (site-scoped)
        var entry = _cache.Find(slice.SiteId, path);
        if (entry is not null)
        {
            ApplyEntry(http, entry, context);
            return;
        }

        // Cache miss — fall back to DB query (site-scoped)
        _log.LogDebug("Cache miss for SiteId={SiteId} Path='{Path}' — querying AeroDB", slice.SiteId, path);

        using var scope = _serviceProvider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var aliases = session.Query<AliasDocument>()
            .Where(x => x.SiteId == slice.SiteId)  // site-scoped
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
                    aliasPath,
                    alias.NewPath),
                    context);
                return;
            }
        }

        // No match in cache or DB
        _log.LogDebug("No alias found for SiteId={SiteId} Path='{Path}'", slice.SiteId, path);
    }

    private static void ApplyEntry(HttpContext http, AliasRuleEntry entry, RewriteContext context)
    {
        http.Response.StatusCode = entry.StatusCode;
        http.Response.Headers[HeaderNames.Location] =
            entry.NewPath + http.Request.QueryString;
        context.Result = RuleResult.EndResponse;
    }

    private static string NormalizePath(string? path)
        => (path ?? "").Trim().TrimEnd('/').ToLowerInvariant();
}
