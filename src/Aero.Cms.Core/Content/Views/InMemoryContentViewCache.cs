using Aero.Cms.Abstractions.Content.Views;

namespace Aero.Cms.Core.Content.Views;

/// <summary>Five-minute default cache with tenant/site-prefixed keys and explicit invalidation.</summary>
public sealed class InMemoryContentViewCache : IContentViewExecutionCache, IContentViewCacheInvalidator, IContentViewCacheGenerationProvider
{
    public const int MaximumEntries = 1_024;
    public const int MaximumGenerations = 1_024;
    private readonly object gate = new();
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<ContentViewScope, long> generations = [];
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(5);

    public Task<ContentViewExecutionResult?> TryGetAsync(string key, CancellationToken ct = default)
    {
        lock (gate)
        {
            RemoveExpired();
            if (entries.TryGetValue(key, out var entry) && entry.ExpiresOn > DateTimeOffset.UtcNow)
                return Task.FromResult<ContentViewExecutionResult?>(entry.Result);
            entries.Remove(key);
            return Task.FromResult<ContentViewExecutionResult?>(null);
        }
    }
    public Task SetAsync(string key, ContentViewExecutionResult result, TimeSpan duration, CancellationToken ct = default)
    {
        lock (gate)
        {
            RemoveExpired();
            while (entries.Count >= MaximumEntries)
            {
                var oldest = entries.MinBy(static pair => pair.Value.CreatedOn);
                entries.Remove(oldest.Key);
            }
            var now = DateTimeOffset.UtcNow;
            entries[key] = new CacheEntry(now, now.Add(duration <= TimeSpan.Zero ? DefaultDuration : duration), result);
        }
        return Task.CompletedTask;
    }
    public Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        lock (gate)
        {
            while (generations.Count >= MaximumGenerations && !generations.ContainsKey(scope))
                generations.Remove(generations.Keys.First());
            generations[scope] = generations.TryGetValue(scope, out var current) ? checked(current + 1) : 1;
            var prefix = ContentViewCacheKeys.ScopePrefix(scope);
            foreach (var key in entries.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) entries.Remove(key);
        }
        return Task.CompletedTask;
    }
    public Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult(generations.TryGetValue(scope, out var generation) ? generation : 0L);
    }
    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in entries.Where(pair => pair.Value.ExpiresOn <= now).Select(pair => pair.Key).ToArray()) entries.Remove(key);
    }
    private sealed record CacheEntry(DateTimeOffset CreatedOn, DateTimeOffset ExpiresOn, ContentViewExecutionResult Result);
}
