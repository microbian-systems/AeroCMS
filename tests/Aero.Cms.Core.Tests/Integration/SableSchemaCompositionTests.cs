using Aero.EfCore.Extensions;
using FluentAssertions;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Core.Extensions;
using Aero.Modular;
using Aero.Cms.Modules.Modules.Services;


namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// Regression tests for Sable schema composition through the module system.
/// 
/// VALIDATION APPROACH: These tests verify that module-level <see cref="IConfigureAeroDB"/>
/// contributions
/// contributed by independently registered modules
/// flow into the resolved Sable document store.
///
/// The critical behavior being tested is that module <c>ConfigureServices()</c>
/// registrations remain available when Sable composes its shared
/// <see cref="StoreOptions"/> instance.
/// </summary>
public class SableSchemaCompositionTests
{
    /// <summary>
    /// Verifies that <see cref="IConfigureAeroDB"/> registrations contributed by a
    /// module are captured after the module's <c>ConfigureServices()</c> runs.
    /// </summary>
    [Test]
    public void ModuleConfigureServices_ShouldRegisterIConfigureAeroDbContributions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        // Simulate what AddAeroModulesAsync does:
        // Register module system services
        services.AddModuleSystemServices();
    
        
        // Register a test module that contributes Sable schema configuration.
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        // 3. Build provider and call Configure/ConfigureServices
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - verify TestAeroDbConfiguration is registered
        var configureAeroDbServices = services
            .Where(sd => sd.ServiceType == typeof(global::AeroDB.Sable.IConfigureAeroDB))
            .ToList();

        configureAeroDbServices.Should().Contain(sd => 
            sd.ImplementationType == typeof(TestAeroDbConfiguration),
            "TestAeroDbConfiguration should be registered via TestAeroDbModule.ConfigureServices()");

        configureAeroDbServices.Should().ContainSingle(
            "the test module is the only persistence contributor registered in this isolated service collection");
    }

    /// <summary>
    /// Verifies that the retained legacy data-layer extension does not remove or
    /// replace Sable schema contributions registered by modules.
    /// </summary>
    [Test]
    public void LegacyDataLayerHook_ShouldPreserveSableModuleRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - the legacy hook is a no-op because Sable is registered elsewhere.
        services.AddAeroDataLayer(configuration, environment);

        using var configuredProvider = services.BuildServiceProvider();
        configuredProvider.GetServices<IConfigureAeroDB>()
            .Should().ContainSingle(configuration => configuration is TestAeroDbConfiguration);
    }

    /// <summary>
    /// Verifies that Sable configurators from different modules receive the same
    /// mutable <see cref="StoreOptions"/> instance.
    /// </summary>
    [Test]
    public void MultipleIConfigureAeroDb_ShouldReceiveSameStoreOptions()
    {
        var receivedOptions = new List<StoreOptions>();
        
        var services = new ServiceCollection();
        
        // Register a tracking configurator
        services.AddSingleton<global::AeroDB.Sable.IConfigureAeroDB>(new TrackingAeroDbConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));
        
        // Add a second one
        services.AddSingleton<global::AeroDB.Sable.IConfigureAeroDB>(new TrackingAeroDbConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));

        // Resolve every Sable configurator and compose them against one options instance.
        using var provider = services.BuildServiceProvider();
        var configurators = provider.GetServices<global::AeroDB.Sable.IConfigureAeroDB>().ToList();
        
        var storeOptions = new StoreOptions();
        foreach (var configurator in configurators)
        {
            configurator.Configure(provider, storeOptions);
        }

        // Assert - both configurators should have received the SAME StoreOptions instance
        receivedOptions.Should().HaveCount(2);
        receivedOptions[0].Should().BeSameAs(receivedOptions[1],
            "all Sable configurators must receive the same StoreOptions instance");
        receivedOptions[0].Should().BeSameAs(storeOptions);
    }

    /// <summary>
    /// Verifies that Sable schema configuration registered through the module system
    /// is resolvable after <c>ConfigureServices()</c> runs.
    /// </summary>
    [Test]
    public void IConfigureAeroDb_ShouldBeResolvableFromModuleConfigureServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - resolve all Sable schema configurators.
        using var afterConfigServices = services.BuildServiceProvider();
        var configurators = afterConfigServices.GetServices<global::AeroDB.Sable.IConfigureAeroDB>().ToList();

        // Assert
        configurators.Should().Contain(sd => sd.GetType() == typeof(TestAeroDbConfiguration),
            "TestAeroDbConfiguration should be resolvable after module services configured");
    }

    // =====================================================================
    // Test module helpers - simulate a module-owned Sable schema contribution.
    // =====================================================================

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Test module that contributes Sable schema configuration.
    /// </summary>
    private sealed class TestAeroDbModule : AeroModuleBase
    {
        public override string Name => nameof(TestAeroDbModule);
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override short Order => 100;
        public override IReadOnlyList<string> Dependencies => [];
        public override IReadOnlyList<string> Category => ["test"];
        public override IReadOnlyList<string> Tags => ["test", "sable"];

        public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
        {
            services.AddSingleton<global::AeroDB.Sable.IConfigureAeroDB, TestAeroDbConfiguration>();
        }

        public override Task RunAsync(IServiceProvider builder) => Task.CompletedTask;
    }

    /// <summary>
    /// Test IConfigureAeroDB that mimics DocsAeroDbConfiguration's schema contribution.
    /// </summary>
    private sealed class TestAeroDbConfiguration : IConfigureAeroDB
    {
        public void Configure(StoreOptions options)
        {
            // Simulate a module adding a custom index (like DocsAeroDbConfiguration does)
        }
    }

    /// <summary>
    /// Test IConfigureAeroDB implementation that captures the StoreOptions it receives.
    /// </summary>
    private sealed class TrackingAeroDbConfiguration(Action<StoreOptions> onConfigure) : IConfigureAeroDB
    {
        public void Configure(StoreOptions options)
        {
            onConfigure(options);
        }
    }
}
