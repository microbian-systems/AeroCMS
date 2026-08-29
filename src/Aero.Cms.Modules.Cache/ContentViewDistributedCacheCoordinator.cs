using Aero.Cms.Abstractions.Content.Views;
using Microsoft.Extensions.Caching.Distributed;

namespace Aero.Cms.Modules.Cache;

/// <summary>
/// Shared generation authority for query-backed content. A generation is kept in the configured
/// distributed cache so every application instance changes its result-cache identity after a
/// publish, import activation, or manual invalidation.
/// </summary>
public sealed class DistributedContentViewCacheCoordinator(IDistributedCache cache)
    : IContentViewDistributedCacheCoordinator
{
    private static readonly TimeSpan GenerationLifetime = TimeSpan.FromDays(30);

    public bool IsDistributed => true;

    public async Task<long> GetGenerationAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return 0;
        var value = await cache.GetStringAsync(Key(scope), ct);
        return long.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var generation)
            ? generation
            : 0;
    }

    public async Task InvalidateAsync(ContentViewScope scope, CancellationToken ct = default)
    {
        if (!scope.IsValid) return;
        // A cryptographically random positive generation is an invalidation token, not a counter.
        // This avoids a read/modify/write race without requiring an unavailable Redis INCR API.
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var candidate = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes)
            & long.MaxValue;
        var generation = candidate == 0 ? 1 : candidate;
        await cache.SetStringAsync(
            Key(scope),
            generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = GenerationLifetime },
            ct);
    }

    private static string Key(ContentViewScope scope)
        => $"content-view:generation:{scope.TenantId}:{scope.SiteId}";
}
