using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Custom <see cref="IRule"/> that evaluates URL aliases from the Marten database
/// on each request. Results are cached for 30 seconds to avoid hitting the DB
/// on every request while still allowing live updates within a reasonable window.
///
/// This is registered as a singleton but creates scoped DI scopes inside
/// <see cref="ApplyRule"/> to resolve scoped <see cref="IDocumentSession"/>.
/// </summary>
public sealed class AliasRewriteRule : IRule
{
    private readonly IMemoryCache _cache;
    private readonly IServiceProvider _serviceProvider;
    private const string CacheKey = "rewrite-aliases";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public AliasRewriteRule(IMemoryCache cache, IServiceProvider serviceProvider)
    {
        _cache = cache;
        _serviceProvider = serviceProvider;
    }

    public void ApplyRule(RewriteContext context)
    {
        var request = context.HttpContext.Request;
        var path = request.Path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(path)) return;

        // Load aliases from cache (refreshed from Marten every 30s)
        var aliases = _cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _serviceProvider.CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            return session.Query<AliasDocument>().ToList();
        });

        if (aliases is null || aliases.Count == 0) return;

        // Check if the current path matches any alias
        foreach (var alias in aliases)
        {
            if (string.Equals(alias.OldPath, path, StringComparison.OrdinalIgnoreCase))
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status301MovedPermanently;
                response.Headers["Location"] = alias.NewPath;
                context.Result = RuleResult.EndResponse;
                return;
            }
        }
    }
}
