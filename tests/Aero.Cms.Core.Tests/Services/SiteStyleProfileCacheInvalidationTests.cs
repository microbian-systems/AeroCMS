using Aero.Cms.Abstractions.Events;
using Aero.Cms.Modules.Cache.Handlers;
using Aero.Cms.Modules.Cache.Services;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Core.Tests.Services;

public sealed class SiteStyleProfileCacheInvalidationTests
{
    [Test]
    public async Task Profile_change_evicts_only_the_owning_sites_rendered_pages()
    {
        var fusionCache = Substitute.For<IFusionCache>();
        var outputCache = Substitute.For<IOutputCacheStore>();
        var service = new FusionCacheInvalidationService(
            fusionCache,
            outputCache,
            Substitute.For<ILogger<FusionCacheInvalidationService>>());
        var changed = new SiteStyleProfileChangedEvent(42, 3, DateTimeOffset.UtcNow);

        await service.InvalidateSiteStyleProfileAsync(changed);

        await outputCache.Received(1).EvictByTagAsync(
            "site-pages-42",
            Arg.Any<CancellationToken>());
        await fusionCache.DidNotReceive()
            .RemoveByTagAsync(
                Arg.Any<string>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Wolverine_handler_routes_profile_changes_to_the_site_scoped_invalidator()
    {
        var invalidator = Substitute.For<ICacheInvalidationService>();
        var handler = new ContentUpdatedHandler(
            invalidator,
            Substitute.For<ILogger<ContentUpdatedHandler>>());
        var changed = new SiteStyleProfileChangedEvent(84, 9, DateTimeOffset.UtcNow);

        await handler.Handle(changed, CancellationToken.None);

        await invalidator.Received(1)
            .InvalidateSiteStyleProfileAsync(changed, CancellationToken.None);
    }

    [Test]
    public async Task Theme_change_evicts_only_the_owning_sites_rendered_pages()
    {
        var fusionCache = Substitute.For<IFusionCache>();
        var outputCache = Substitute.For<IOutputCacheStore>();
        var service = new FusionCacheInvalidationService(
            fusionCache,
            outputCache,
            Substitute.For<ILogger<FusionCacheInvalidationService>>());
        var changed = new SiteThemeChangedEvent(42, "ocean", "2.1.0", 3, DateTimeOffset.UtcNow);

        await service.InvalidateSiteThemeAsync(changed);

        await outputCache.Received(1).EvictByTagAsync(
            "site-pages-42",
            Arg.Any<CancellationToken>());
        await fusionCache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Wolverine_handler_routes_theme_changes_to_the_site_scoped_invalidator()
    {
        var invalidator = Substitute.For<ICacheInvalidationService>();
        var handler = new ContentUpdatedHandler(
            invalidator,
            Substitute.For<ILogger<ContentUpdatedHandler>>());
        var changed = new SiteThemeChangedEvent(84, "ocean", "2.1.0", 9, DateTimeOffset.UtcNow);

        await handler.Handle(changed, CancellationToken.None);

        await invalidator.Received(1)
            .InvalidateSiteThemeAsync(changed, CancellationToken.None);
    }
}
