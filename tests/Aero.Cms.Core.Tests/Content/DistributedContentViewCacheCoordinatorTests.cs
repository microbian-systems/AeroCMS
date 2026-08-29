using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Modules.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class DistributedContentViewCacheCoordinatorTests
{
    [Test]
    public async Task Invalidation_changes_the_shared_site_generation_without_crossing_scope()
    {
        var distributed = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var firstNode = new DistributedContentViewCacheCoordinator(distributed);
        var secondNode = new DistributedContentViewCacheCoordinator(distributed);
        var scope = new ContentViewScope(71, 42);
        var other = new ContentViewScope(71, 43);

        (await firstNode.GetGenerationAsync(scope)).ShouldBe(0);
        await firstNode.InvalidateAsync(scope);

        (await secondNode.GetGenerationAsync(scope)).ShouldBeGreaterThan(0);
        (await secondNode.GetGenerationAsync(other)).ShouldBe(0);
    }
}
