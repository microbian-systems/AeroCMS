using TUnit.Core;
using Aero.Cms.Core.Tests.TestModules;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Cms.Modules.Modules.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// Integration tests for module host integration and startup behavior.
/// </summary>
public class ModuleHostIntegrationTests
{
    [Test]
    public async Task ModuleLifecycle_ConfigureServices_ShouldBeCalled()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        // Register modules explicitly for testing
        services.AddSingleton<IAeroModule, SimpleTestModule>();

        // Act - simulate what AddAeroModulesAsync does
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        await using var tempProvider = services.BuildServiceProvider();
        var modules = tempProvider.GetServices<IAeroModule>().ToList();

        foreach (var module in modules)
        {
            module.Configure(moduleBuilder);
        }

        foreach (var module in modules.OfType<SimpleTestModule>())
        {
            module.ConfigureServices(services, configuration, environment);
        }

        // Assert
        var simpleModule = modules.OfType<SimpleTestModule>().First();
        simpleModule.ConfigureWasCalled.Should().BeTrue();
        simpleModule.ConfigureServicesWasCalled.Should().BeTrue();
    }

    [Test]
    public async Task ModuleLifecycle_WithWebHost_ShouldExecuteRun()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        // Add test module
        builder.Services.AddSingleton<IAeroModule, SimpleTestModule>();

        // Act
        var app = builder.Build();
        var modules = app.Services.GetServices<IAeroModule>().OfType<SimpleTestModule>().ToList();

        // Execute Run method as the extension method does
        foreach (var module in modules)
        {
            module.Run(app);
        }

        // Assert
        modules.First().RunWasCalled.Should().BeTrue();
    }

    [Test]
    public async Task ModuleDiscovery_Scanning_ShouldFindAllModules()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act - Use Scrutor scanning as the real implementation does
        services.Scan(scan => scan
            .FromAssemblyOf<SimpleTestModule>()
            .AddClasses(classes => classes.AssignableTo<IAeroModule>())
            .AsImplementedInterfaces()
            .AsSelf()
            .WithSingletonLifetime());

        await using var provider = services.BuildServiceProvider();
        var modules = provider.GetServices<IAeroModule>().ToList();

        // Assert - should find all test modules
        modules.Should().Contain(m => m.Name == "SimpleTest");
        modules.Should().Contain(m => m.Name == "DependentTest");
        modules.Should().Contain(m => m.Name == "MultiDependency");
    }

    [Test]
    public async Task ModuleOrdering_ShouldRespectOrderProperty()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add modules in random order
        services.AddSingleton<IAeroModule, LateLoadingTestModule>();
        services.AddSingleton<IAeroModule, SimpleTestModule>();
        services.AddSingleton<IAeroModule, EarlyLoadingTestModule>();

        // Act
        await using var provider = services.BuildServiceProvider();
        var modules = provider.GetServices<IAeroModule>().OrderBy(m => m.Order).ToList();

        // Assert
        modules[0].Name.Should().Be("EarlyLoading");  // Order = -100
        modules[1].Name.Should().Be("SimpleTest");    // Order = 0
        modules[2].Name.Should().Be("LateLoading");   // Order = 1000
    }

    [Test]
    public async Task ModuleSpecialization_UiModules_ShouldBeDiscoverable()
    {
        // Arrange
        var services = new ServiceCollection();

        services.Scan(scan => scan
            .FromAssemblyOf<UiTestModule>()
            .AddClasses(classes => classes.AssignableTo<IUiModule>())
            .AsImplementedInterfaces()
            .AsSelf()
            .WithSingletonLifetime());

        // Act
        await using var provider = services.BuildServiceProvider();
        var uiModules = provider.GetServices<IUiModule>().ToList();

        // Assert
        uiModules.Should().ContainSingle(m => m.Name == "UiTest");
    }

    [Test]
    public async Task ModuleSpecialization_ApiModules_ShouldBeDiscoverable()
    {
        // Arrange
        var services = new ServiceCollection();

        services.Scan(scan => scan
            .FromAssemblyOf<ApiTestModule>()
            .AddClasses(classes => classes.AssignableTo<IApiModule>())
            .AsImplementedInterfaces()
            .AsSelf()
            .WithSingletonLifetime());

        // Act
        await using var provider = services.BuildServiceProvider();
        var apiModules = provider.GetServices<IApiModule>().ToList();

        // Assert
        apiModules.Should().ContainSingle(m => m.Name == "ApiTest");
    }

    [Test]
    public async Task ModuleSpecialization_BackgroundModules_ShouldBeDiscoverable()
    {
        // Arrange
        var services = new ServiceCollection();

        services.Scan(scan => scan
            .FromAssemblyOf<BackgroundTestModule>()
            .AddClasses(classes => classes.AssignableTo<IBackgroundModule>())
            .AsImplementedInterfaces()
            .AsSelf()
            .WithSingletonLifetime());

        // Act
        await using var provider = services.BuildServiceProvider();
        var backgroundModules = provider.GetServices<IBackgroundModule>().ToList();

        // Assert
        backgroundModules.Should().ContainSingle(m => m.Name == "BackgroundTest");
    }

    [Test]
    public async Task ModuleBuilder_ShouldBePassedToConfigure()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new FakeHostEnvironment();

        // Track if builder was received
        IAeroModuleBuilder? capturedBuilder = null;
        var testModule = new CallbackTestModule(builder => capturedBuilder = builder);

        services.AddSingleton<IAeroModule>(testModule);

        // Act
        var moduleBuilder = new AeroModuleBuilder(services, configuration, environment);
        testModule.Configure(moduleBuilder);

        // Assert
        capturedBuilder.Should().NotBeNull();
        capturedBuilder.Should().Be(moduleBuilder);
    }

    [Test]
    public async Task ModuleLifecycle_Dispose_ShouldBeCallable()
    {
        // Arrange
        var module = new SimpleTestModule();

        // Act
        module.Dispose();

        // Assert - should not throw
        true.Should().BeTrue();
    }

    [Test]
    public async Task ModuleBase_Properties_ShouldReturnExpectedValues()
    {
        // Arrange & Act
        var module = new SimpleTestModule();

        // Assert
        module.Name.Should().Be("SimpleTest");
        module.Version.Should().Be("1.0.0");
        module.Author.Should().Be("TestAuthor");
        module.Order.Should().Be(0);
        module.DisabledInProduction.Should().BeFalse();
        module.Dependencies.Should().BeEmpty();
        module.Category.Should().ContainSingle().Which.Should().Be("Test");
        module.Tags.Should().Contain("test");
        module.Tags.Should().Contain("simple");
        await Task.CompletedTask;
    }

    [Test]
    public async Task RunAsync_DefaultImplementation_ShouldCompleteSynchronously()
    {
        // Arrange
        var module = new SimpleTestModule();
        var builder = new TestEndpointRouteBuilder();

        // Act
        var task = module.RunAsync(builder);
        await task;

        // Assert
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    /// <summary>
    /// Fake host environment for testing.
    /// </summary>
    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Test module that captures the builder.
    /// </summary>
    private class CallbackTestModule(Action<IAeroModuleBuilder> callback) : AeroModuleBase
    {
        public override string Name => "CallbackTest";
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public override IReadOnlyList<string> Category => Array.Empty<string>();
        public override IReadOnlyList<string> Tags => Array.Empty<string>();

        public override void Configure(IAeroModuleBuilder builder)
        {
            callback(builder);
            base.Configure(builder);
        }
    }

    /// <summary>
    /// Minimal implementation of IEndpointRouteBuilder for testing.
    /// </summary>
    private class TestEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
        public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

        public IApplicationBuilder CreateApplicationBuilder()
        {
            throw new NotImplementedException();
}
    }
}
