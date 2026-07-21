using Aero.Cms.Modules.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class CacheModuleRegistrationTests
{
    [Test]
    public void Local_mode_keeps_distributed_cache_without_attaching_a_backplane()
    {
        var services = ConfigureServices("Local");

        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IFusionCacheBackplane));

        services.AddSingleton(Substitute.For<IFusionCacheBackplane>());
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IFusionCache>();

        cache.HasDistributedCache.ShouldBeTrue();
        cache.HasBackplane.ShouldBeFalse();
    }

    [Test]
    public void Server_mode_keeps_distributed_cache_and_attaches_the_registered_backplane()
    {
        var services = ConfigureServices("Server");

        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IFusionCacheBackplane));

        services.RemoveAll<IFusionCacheBackplane>();
        services.AddSingleton(Substitute.For<IFusionCacheBackplane>());
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IFusionCache>();

        cache.HasDistributedCache.ShouldBeTrue();
        cache.HasBackplane.ShouldBeTrue();
    }

    private static ServiceCollection ConfigureServices(string cacheMode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:Bootstrap:CacheMode"] = cacheMode,
                ["ConnectionStrings:cache"] = "localhost:33333"
            })
            .Build();
        var services = new ServiceCollection();

        new CacheModule().ConfigureServices(services, configuration);

        return services;
    }
}
