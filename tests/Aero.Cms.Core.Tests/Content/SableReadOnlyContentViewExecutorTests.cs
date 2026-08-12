using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Views;
using AeroDB.Sable;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class SableReadOnlyContentViewExecutorTests
{
    [Test]
    public void Dedicated_executor_is_disabled_without_explicit_store_configuration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContentViewSourceRegistry>(new Sources(false));
        services.AddSableReadOnlyContentViews(_ => { });
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IReadOnlyContentViewExecutor>();
        executor.IsReadOnlyGuaranteed.ShouldBeFalse();
        provider.GetService<IDocumentStore>().ShouldBeNull();
    }

    [Test]
    public void Explicit_host_factory_requires_registered_sources_and_pre_materialization_transport()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContentViewSourceRegistry>(new Sources(true));
        services.AddSableReadOnlyContentViews(options => options.UseHostResolvedStoreFactory = true);
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IReadOnlyContentViewExecutor>();
        executor.IsReadOnlyGuaranteed.ShouldBeFalse();
        provider.GetService<IDocumentStore>().ShouldBeNull();
    }

    [Test]
    public void Explicit_host_factory_becomes_available_only_with_a_bounded_transport()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContentViewSourceRegistry>(new Sources(true));
        services.AddSingleton<IContentViewBoundedQueryTransport>(new BoundedTransport());
        services.AddSableReadOnlyContentViews(options => options.UseHostResolvedStoreFactory = true);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IReadOnlyContentViewExecutor>().IsReadOnlyGuaranteed.ShouldBeTrue();
    }

    private sealed class Sources(bool hasSources) : IContentViewSourceRegistry
    {
        public bool IsValid => true;
        public bool HasSources => hasSources;
        public IReadOnlyList<string> Errors => [];
        public bool TryGetByTable(string table, out ContentViewSourceDefinition? source) { source = null; return false; }
    }

    private sealed class BoundedTransport : IContentViewBoundedQueryTransport
    {
        public bool EnforcesLimitsBeforeMaterialization => true;
        public Task<ContentViewExecutionResult> ExecuteBoundedAsync(ContentViewExecutionRequest request, CancellationToken ct = default)
            => Task.FromResult(new ContentViewExecutionResult([], false));
    }
}
