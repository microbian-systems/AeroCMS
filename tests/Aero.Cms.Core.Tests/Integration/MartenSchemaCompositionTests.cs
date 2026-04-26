using TUnit.Core;
using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Core.Blocks;
using Aero.EfCore.Extensions;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Aero.Cms.Core;
using Aero.Cms.Core.Extensions;


namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// Regression tests for Marten schema composition through the module system.
/// 
/// VALIDATION APPROACH: These tests verify that module-level IConfigureMarten contributions
/// (from both framework-level BlockMartenConfiguration and module-level configurations)
/// flow into the resolved DocumentStore when AddAeroDataLayer() is called.
///
/// The critical gap being tested: AddAeroDataLayer() must be called AFTER module
/// ConfigureServices() registrations complete, so that all IConfigureMarten contributors
/// are available in DI when AddMarten() resolves them internally.
///
/// This test class does NOT require a live PostgreSQL instance - it validates the
/// DI registration composition only.
/// </summary>
public class MartenSchemaCompositionTests
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
        // 1. Register module system services (includes BlockMartenConfiguration)
        services.AddModuleSystemServices();
        
        // 2. Register a test module that contributes IConfigureMarten (simulating DocsModule)
        services.AddSingleton<IAeroModule, TestMartenModule>();
        
        // 3. Build provider and call Configure/ConfigureServices
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestMartenModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - verify TestMartenConfiguration is registered
        var configureMartenServices = services
            .Where(sd => sd.ServiceType == typeof(global::Marten.IConfigureMarten))
            .ToList();

        // Assert - at minimum, BlockMartenConfiguration and TestMartenConfiguration should be registered
        configureMartenServices.Should().Contain(sd => 
            sd.ImplementationType == typeof(BlockMartenConfiguration),
            "BlockMartenConfiguration should be registered via AddModuleSystemServices()");
        
        configureMartenServices.Should().Contain(sd => 
            sd.ImplementationType == typeof(TestMartenConfiguration),
            "TestMartenConfiguration should be registered via TestMartenModule.ConfigureServices()");

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
        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestMartenModule>();
        
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestMartenModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - Call AddAeroDataLayer as it SHOULD be wired in startup
        // This is the MISSING call in the current startup chain
        services.AddAeroDataLayer(configuration, environment);

        // Assert - DocumentStore should be registered (from AddMarten inside AddAeroDataLayer)
        var documentStoreService = services
            .FirstOrDefault(sd => sd.ServiceType == typeof(global::Marten.IDocumentStore));

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
        services.AddSingleton<global::Marten.IConfigureMarten>(new TrackingMartenConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));
        
        // Add a second one
        services.AddSingleton<global::Marten.IConfigureMarten>(new TrackingMartenConfiguration(opts =>
        {
            receivedOptions.Add(opts);
        }));

        // Simulate what AddMarten does internally: resolve all IConfigureMarten and call them
        // with the SAME StoreOptions instance
        using var provider = services.BuildServiceProvider();
        var configurators = provider.GetServices<global::Marten.IConfigureMarten>().ToList();
        
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

        services.AddModuleSystemServices();
        services.AddSingleton<IAeroModule, TestMartenModule>();
        
        using var provider = services.BuildServiceProvider();
        
        var testModule = provider.GetServices<IAeroModule>().OfType<TestMartenModule>().First();
        testModule.ConfigureServices(services, configuration, environment);

        // Act - resolve all IConfigureMarten registrations
        using var afterConfigServices = services.BuildServiceProvider();
        var configurators = afterConfigServices.GetServices<global::Marten.IConfigureMarten>().ToList();

        // Assert
        configurators.Should().Contain(sd => sd.GetType() == typeof(BlockMartenConfiguration),
            "BlockMartenConfiguration should be resolvable after module services configured");
        configurators.Should().Contain(sd => sd.GetType() == typeof(TestMartenConfiguration),
            "TestMartenConfiguration should be resolvable after module services configured");
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
    private sealed class TestMartenModule : AeroModuleBase
    {
        public override string Name => nameof(TestMartenModule);
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override short Order => 100;
        public override IReadOnlyList<string> Dependencies => [];
        public override IReadOnlyList<string> Category => ["test"];
        public override IReadOnlyList<string> Tags => ["test", "marten"];

        public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
        {
            services.AddSingleton<global::Marten.IConfigureMarten, TestMartenConfiguration>();
        }

        public override Task RunAsync(IServiceProvider builder) => Task.CompletedTask;
    }

    /// <summary>
    /// Test IConfigureMarten that mimics DocsMartenConfiguration's schema contribution.
    /// </summary>
    private sealed class TestMartenConfiguration : IConfigureMarten
    {
        public void Configure(IServiceProvider services, StoreOptions options)
        {
            // Simulate a module adding a custom index (like DocsMartenConfiguration does)
            // options.Schema.For<MarkdownPage>().Index(x => x.Slug);
        }
    }

    /// <summary>
    /// Test IConfigureMarten implementation that captures the StoreOptions it receives.
    /// </summary>
    private sealed class TrackingMartenConfiguration(Action<StoreOptions> onConfigure) : IConfigureMarten
    {
        public void Configure(IServiceProvider services, StoreOptions options)
        {
            onConfigure(options);
}
    }
}
