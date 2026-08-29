using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Cms.Modules.Aliases;
using Microsoft.AspNetCore.Builder;
using Aero.Cms.Modules.Sites;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class RuntimeBootstrapReadinessGateTests
{
    [Test]
    public async Task Configured_request_waits_until_runtime_is_ready_before_continuing()
    {
        var readiness = new RuntimeBootstrapReadinessGate(requiresReadiness: true);
        var middleware = CreateMiddleware(readiness);
        var context = new DefaultHttpContext();
        var reachedNext = false;

        var request = middleware.InvokeAsync(context, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        await Task.Yield();
        reachedNext.ShouldBeFalse();

        readiness.SignalReady();
        await request;

        reachedNext.ShouldBeTrue();
    }

    [Test]
    public async Task Failed_configured_request_returns_plain_503_without_calling_downstream()
    {
        var readiness = new RuntimeBootstrapReadinessGate(requiresReadiness: true);
        readiness.SignalFailure();
        var middleware = CreateMiddleware(readiness);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var reachedNext = false;

        await middleware.InvokeAsync(context, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        context.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.ShouldBe("text/plain");
        reachedNext.ShouldBeFalse();
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).ShouldBe("Service Unavailable");
    }

    [Test]
    public async Task Configured_allowlisted_health_request_bypasses_readiness()
    {
        var middleware = CreateMiddleware(new RuntimeBootstrapReadinessGate(requiresReadiness: true));
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        var reachedNext = false;

        await middleware.InvokeAsync(context, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        reachedNext.ShouldBeTrue();
    }

    [Test]
    [Arguments("/lib/site.js")]
    [Arguments("/assets/logo.svg")]
    [Arguments("/media/hero.webp")]
    public async Task Configured_static_asset_prefixes_bypass_readiness(string path)
    {
        var middleware = CreateMiddleware(new RuntimeBootstrapReadinessGate(requiresReadiness: true));
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var reachedNext = false;

        await middleware.InvokeAsync(context, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        reachedNext.ShouldBeTrue();
    }

    [Test]
    public async Task Process_start_configured_gate_remains_pending_when_mutable_state_becomes_running()
    {
        var readiness = new RuntimeBootstrapReadinessGate(requiresReadiness: true);
        var middleware = CreateSetupGateMiddleware(readiness, BootstrapStates.Running);
        var context = new DefaultHttpContext();
        var reachedNext = false;

        var request = middleware.InvokeAsync(context, _ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });

        await Task.Yield();
        reachedNext.ShouldBeFalse();

        readiness.SignalReady();
        await request;
        reachedNext.ShouldBeTrue();
    }

    [Test]
    public async Task Readiness_filter_ordering_helper_moves_an_explicit_registration_before_later_filters()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        new SetupModule().ConfigureServices(services, configuration, environment);
        new SitesModule().ConfigureServices(services, configuration, environment);
        new AliasModule().ConfigureServices(services, configuration, environment);
        services.AddTransient<IStartupFilter, RuntimeBootstrapReadinessStartupFilter>();
        services.AddTransient<IStartupFilter, SiteStartupFilter>();
        services.AddTransient<IStartupFilter, AliasStartupFilter>();
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, LaterStartupFilter>());
        RuntimeBootstrapReadinessStartupFilterOrdering.MoveReadinessFilterToStart(services);

        var startupFilters = services
            .Select((descriptor, index) => (descriptor, index))
            .Where(entry => entry.descriptor.ServiceType == typeof(IStartupFilter))
            .ToArray();
        var readinessIndex = startupFilters.Single(entry =>
            entry.descriptor.ImplementationType == typeof(RuntimeBootstrapReadinessStartupFilter)).index;
        var siteFilter = startupFilters.Single(entry =>
            entry.descriptor.ImplementationType == typeof(SiteStartupFilter));
        var aliasFilter = startupFilters.Single(entry =>
            entry.descriptor.ImplementationType == typeof(AliasStartupFilter));
        var laterFilter = startupFilters.Single(entry =>
            entry.descriptor.ImplementationType == typeof(LaterStartupFilter));

        readinessIndex.ShouldBe(0);
        siteFilter.index.ShouldBeGreaterThan(readinessIndex);
        aliasFilter.index.ShouldBeGreaterThan(readinessIndex);
        laterFilter.index.ShouldBeGreaterThan(readinessIndex);
        await Task.CompletedTask;
    }

    private static RuntimeBootstrapReadinessMiddleware CreateMiddleware(RuntimeBootstrapReadinessGate readiness)
    {
        return new RuntimeBootstrapReadinessMiddleware(new SetupPathAllowlist(), readiness);
    }

    private static SetupGateMiddleware CreateSetupGateMiddleware(
        RuntimeBootstrapReadinessGate readiness,
        string state)
    {
        var setup = Substitute.For<ISetupInitializationService>();
        setup.GetBootstrapState().Returns(new BootstrapState { State = state });
        return new SetupGateMiddleware(setup, new SetupPathAllowlist(), readiness);
    }

    private sealed class LaterStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => next;
    }
}
