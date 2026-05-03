using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Custom <see cref="IRule"/> that evaluates URL aliases.
///
/// Primary path: reads from the in-memory <see cref="IAliasRuleCache"/> (zero DB I/O).
/// Cache-miss fallback: queries Marten directly, then populates the cache.
///
/// Warmup: <see cref="AliasRuleCacheWarmupService"/> loads the cache from the DB on startup.
/// Invalidation: cache is invalidated on create/update/delete via <see cref="IAliasRuleCache.Invalidate"/>.
///
/// Registered as a singleton and added to <see cref="RewriteOptions"/>
/// via the <see cref="AliasPipelineStartupFilter"/>.
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

        var path = rawPath.Trim().TrimEnd('/').ToLowerInvariant();

        // Fast path — check in-memory cache
        var entry = _cache.Find(path);
        if (entry is not null)
        {
            ApplyEntry(http, entry, context);
            return;
        }

        // Cache miss — fall back to DB query (logged at Debug for diagnostics)
        _log.LogDebug("Cache miss for path '{Path}' — querying Marten", path);

        using var scope = _serviceProvider.CreateScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var aliases = session.Query<AliasDocument>().ToList();

        foreach (var alias in aliases)
        {
            var aliasPath = (alias.OldPath ?? "").Trim().TrimEnd('/').ToLowerInvariant();
            if (string.Equals(aliasPath, path, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogDebug("DB fallback matched '{OldPath}' → '{NewPath}' for '{Path}'",
                    alias.OldPath, alias.NewPath, path);

                ApplyEntry(http, new AliasRuleEntry(
                    alias.SiteId,
                    aliasPath,
                    alias.NewPath),
                    context);
                return;
            }
        }

        // No match in cache or DB
        _log.LogDebug("No alias found for path '{Path}' (cache miss + DB miss)", path);
    }

    private static void ApplyEntry(HttpContext http, AliasRuleEntry entry, RewriteContext context)
    {
        http.Response.StatusCode = entry.StatusCode;
        http.Response.Headers[HeaderNames.Location] =
            entry.NewPath + http.Request.QueryString;
        context.Result = RuleResult.EndResponse;
    }
}
