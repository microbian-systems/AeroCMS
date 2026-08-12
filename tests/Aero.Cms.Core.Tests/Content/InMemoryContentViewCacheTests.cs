using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class InMemoryContentViewCacheTests
{
    [Test]
    public async Task High_cardinality_keys_are_size_bounded_and_scoped_invalidation_isolated()
    {
        var cache = new InMemoryContentViewCache();
        var result = new ContentViewExecutionResult([], false);
        var firstScope = new ContentViewScope(1, 1);
        var secondScope = new ContentViewScope(1, 2);
        for (var index = 0; index < InMemoryContentViewCache.MaximumEntries + 50; index++)
            await cache.SetAsync(ContentViewCacheKeys.Create(firstScope, "catalog", 1, 0, index.ToString()), result, TimeSpan.FromMinutes(5));
        await cache.SetAsync(ContentViewCacheKeys.Create(secondScope, "catalog", 1, 0, "kept"), result, TimeSpan.FromMinutes(5));

        await cache.InvalidateAsync(firstScope);

        (await cache.TryGetAsync(ContentViewCacheKeys.Create(secondScope, "catalog", 1, 0, "kept"))).ShouldNotBeNull();
    }

    [Test]
    public async Task Expired_entries_are_removed_on_subsequent_cache_access()
    {
        var cache = new InMemoryContentViewCache();
        var key = ContentViewCacheKeys.Create(new ContentViewScope(1, 1), "catalog", 1, 0, "expired");
        await cache.SetAsync(key, new ContentViewExecutionResult([], false), TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        (await cache.TryGetAsync(key)).ShouldBeNull();
    }
}
