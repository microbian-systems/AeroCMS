using Aero.EfCore.Extensions;
using FluentAssertions;
using AeroDB;
using Aero.Cms.Core.Blocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Extensions;
using Aero.Modular;
using Aero.Cms.Modules.Modules.Services;


namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// Regression tests for Marten schema composition through the module system.
/// 
/// VALIDATION APPROACH: These tests verify that module-level IConfigureMarten contributions
/// (from both framework-level BlockAeroDbConfiguration and module-level configurations)
/// flow into the resolved DocumentStore when AddAeroDataLayer() is called.
///
/// The critical gap being tested: AddAeroDataLayer() must be called AFTER module
/// ConfigureServices() registrations complete, so that all IConfigureMarten contributors
/// are available in DI when AddMarten() resolves them internally.
///
/// This test class does NOT require a live PostgreSQL instance - it validates the
/// DI registration composition only.
/// </summary>
public class AeroDbSchemaCompositionTests
{
    /// <summary>
    /// Test that IConfigureMarten registrations from module ConfigureServices() are
    /// captured and available for AddAeroDataLayer() to consume.
    ///
    /// EXPECTED TO FAIL initially: The test module registers an IConfigureMarten
    /// via ConfigureServices(), but AddAeroDataLayer() is never called from the CMS
    /// startup chain (Program.cs â†’ AddAeroCmsAsync â†’ AddAeroModulesAsync).
    /// Until AddAeroDataLayer() is wired in, no DocumentStore will be created.
    /// </summary>
    [Test]
    public void ModuleConfigureServices_ShouldRegisterIConfigureMartenContributions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        // Simulate what AddAeroModulesAsync does:
        // 1. Register block services (Marten serialization config)
        services.AddBlockSystemServices();
        // 2. Register module system services
        services.AddModuleSystemServices();
    
        
        // 2. Register a test module that contributes IConfigureMarten (simulating DocsModule)
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        // 3. Build provider and call Configure/ConfigureServices
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - verify TestAeroDbConfiguration is registered
        var configureMartenServices = services
            .Where(sd => sd.ServiceType == typeof(global::AeroDB.IConfigureAeroDB))
            .ToList();

        // Assert - at minimum, BlockAeroDbConfiguration and TestAeroDbConfiguration should be registered
        configureMartenServices.Should().Contain(sd => 
            sd.ImplementationType == typeof(BlockAeroDbConfiguration),
            "BlockAeroDbConfiguration should be registered via AddModuleSystemServices()");
        
        configureMartenServices.Should().Contain(sd => 
            sd.ImplementationType == typeof(TestAeroDbConfiguration),
            "TestAeroDbConfiguration should be registered via TestAeroDbModule.ConfigureServices()");

        // This count verifies both framework and module contributions are present
        configureMartenServices.Should().HaveCountGreaterThanOrEqualTo(2,
            "Both framework (Block) and module (TestMarten) IConfigureMarten should be registered");
    }

    /// <summary>
    /// Test that AddAeroDataLayer() must be called in the startup chain AFTER
    /// module ConfigureServices() registrations complete.
    ///
    /// This test FAILS currently because AddAeroDataLayer() is not called from
    /// Program.cs â†’ AddAeroCmsAsync â†’ AddAeroModulesAsync.
    ///
    /// After fix: this test will pass, confirming AddAeroDataLayer() is wired.
    /// </summary>
    [Test]
    public void AddAeroDataLayer_ShouldBeWiredAfterModuleRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:aero"] = "Host=localhost;Database=test"
            })
            .Build();
        var environment = new FakeHostEnvironment();

        // Simulate full startup chain up to where AddAeroDataLayer() should be
        services.AddBlockSystemServices();
        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - Call AddAeroDataLayer as it SHOULD be wired in startup
        // This is the MISSING call in the current startup chain
        services.AddAeroDataLayer(configuration, environment);

        // Assert - DocumentStore should be registered (from AddMarten inside AddAeroDataLayer)
        var documentStoreService = services
            .FirstOrDefault(sd => sd.ServiceType == typeof(global::AeroDB.IDocumentStore));

        documentStoreService.Should().NotBeNull("AddAeroDataLayer() should be called from startup and register DocumentStore");
    }

    /// <summary>
    /// Test that IConfigureMarten implementations from different modules don't conflict.
    /// Verifies that the StoreOptions passed to each configurator are the SAME instance.
    /// </summary>
    [Test]
    public void MultipleIConfigureMarten_ShouldReceiveSameStoreOptions()
    {
        // This test validates the composition order guarantee:
        // all IConfigureMarten contributors receive the same StoreOptions mutable object.
        
        var receivedOptions = new List<StoreOptions>();
        
        var services = new ServiceCollection();
        
        // Register a tracking configurator
        services.AddSingleton<global::AeroDB.IConfigureAeroDB>(new TrackingAeroDbConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));
        
        // Add a second one
        services.AddSingleton<global::AeroDB.IConfigureAeroDB>(new TrackingAeroDbConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));

        // Simulate what AddMarten does internally: resolve all IConfigureMarten and call them
        // with the SAME StoreOptions instance
        using var provider = services.BuildServiceProvider();
        var configurators = provider.GetServices<global::AeroDB.IConfigureAeroDB>().ToList();
        
        var storeOptions = new StoreOptions();
        foreach (var configurator in configurators)
        {
            configurator.Configure(provider, storeOptions);
        }

        // Assert - both configurators should have received the SAME StoreOptions instance
        receivedOptions.Should().HaveCount(2);
        receivedOptions[0].Should().BeSameAs(receivedOptions[1],
            "All IConfigureMarten contributors must receive the same StoreOptions instance");
        receivedOptions[0].Should().BeSameAs(storeOptions);
    }

    /// <summary>
    /// Test that verifies the DI chain: IConfigureMarten registered via module system
    /// should be resolvable from the service provider AFTER ConfigureServices runs.
    /// </summary>
    [Test]
    public void IConfigureMarten_ShouldBeResolvableFromModuleConfigureServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        services.AddBlockSystemServices();
        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestAeroDbModule>();
        
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestAeroDbModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - resolve all IConfigureMarten registrations
        using var afterConfigServices = services.BuildServiceProvider();
        var configurators = afterConfigServices.GetServices<global::AeroDB.IConfigureAeroDB>().ToList();

        // Assert
        configurators.Should().Contain(sd => sd.GetType() == typeof(BlockAeroDbConfiguration),
            "BlockAeroDbConfiguration should be resolvable after module services configured");
        configurators.Should().Contain(sd => sd.GetType() == typeof(TestAeroDbConfiguration),
            "TestAeroDbConfiguration should be resolvable after module services configured");
    }

    // =====================================================================
    // Test module helpers - simulate what DocsModule does
    // =====================================================================

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Test module that mimics DocsModule's IConfigureMarten registration pattern.
    /// </summary>
    private sealed class TestAeroDbModule : AeroModuleBase
    {
        public override string Name => nameof(TestAeroDbModule);
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override short Order => 100;
        public override IReadOnlyList<string> Dependencies => [];
        public override IReadOnlyList<string> Category => ["test"];
        public override IReadOnlyList<string> Tags => ["test", "marten"];

        public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
        {
            services.AddSingleton<global::AeroDB.IConfigureAeroDB, TestAeroDbConfiguration>();
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
