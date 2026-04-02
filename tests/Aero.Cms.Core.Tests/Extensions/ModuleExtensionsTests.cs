using Aero.Cms.Core.Extensions;
using Aero.Cms.Core.Tests.TestModules;
using Aero.Cms.Web.Core.Modules;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Tests.Extensions;

/// <summary>
/// Tests for ModuleExtensions integration with the existing extension methods.
/// </summary>
public class ModuleExtensionsTests
{
    [Test]
    public async Task GetModules_Generic_ShouldReturnOnlySpecifiedType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAeroModule, SimpleTestModule>();
        services.AddSingleton<IAeroModule, UiTestModule>();
        services.AddSingleton<IUiModule, UiTestModule>();
        services.AddSingleton<IAeroModule, ApiTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var uiModules = provider.GetModules<IUiModule>().ToList();

        uiModules.Should().ContainSingle();
        uiModules.First().Name.Should().Be("UiTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetModules_ShouldBeOrderedByOrderProperty()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAeroModule, LateLoadingTestModule>(); // Order 1000
        services.AddSingleton<IAeroModule, EarlyLoadingTestModule>(); // Order -100
        services.AddSingleton<IAeroModule, SimpleTestModule>(); // Order 0

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetModules<IAeroModule>().ToList();

        modules[0].Name.Should().Be("EarlyLoading");
        modules[1].Name.Should().Be("SimpleTest");
        modules[2].Name.Should().Be("LateLoading");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetUiModules_ShouldReturnOnlyUiModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAeroModule, SimpleTestModule>();
        services.AddSingleton<IAeroModule, UiTestModule>();
        services.AddSingleton<IUiModule, UiTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetUiModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "UiTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetApiModules_ShouldReturnOnlyApiModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAeroModule, SimpleTestModule>();
        services.AddSingleton<IAeroModule, ApiTestModule>();
        services.AddSingleton<IApiModule, ApiTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetApiModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "ApiTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetBackgroundModules_ShouldReturnOnlyBackgroundModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAeroModule, SimpleTestModule>();
        services.AddSingleton<IAeroModule, BackgroundTestModule>();
        services.AddSingleton<IBackgroundModule, BackgroundTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetBackgroundModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "BackgroundTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetThemeModules_ShouldReturnOnlyThemeModules()
    {
        // Arrange - using concrete implementation that implements IThemeModule
        var services = new ServiceCollection();
        services.AddSingleton<IThemeModule, ThemeTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetThemeModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "ThemeTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetAdminModules_ShouldReturnOnlyAdminModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IAdminModule, AdminTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetAdminModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "AdminTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetFilterModules_ShouldReturnOnlyFilterModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IFilterModule, FilterTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetFilterModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "FilterTest");
        await Task.CompletedTask;
    }

    [Test]
    public async Task GetContentDefinitionModules_ShouldReturnOnlyContentDefinitionModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IContentDefinitionModule, ContentDefinitionTestModule>();

        using var provider = services.BuildServiceProvider();

        // Act
        var modules = provider.GetContentDefinitionModules().ToList();

        modules.Should().ContainSingle(m => m.Name == "ContentDefinitionTest");
        await Task.CompletedTask;
    }

    // Test module implementations for specialized interfaces
    private class ThemeTestModule : AeroModuleBase, IThemeModule
    {
        public override string Name => "ThemeTest";
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public override IReadOnlyList<string> Category => Array.Empty<string>();
        public override IReadOnlyList<string> Tags => Array.Empty<string>();
    }

    private class AdminTestModule : AeroModuleBase, IAdminModule
    {
        public override string Name => "AdminTest";
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public override IReadOnlyList<string> Category => Array.Empty<string>();
        public override IReadOnlyList<string> Tags => Array.Empty<string>();
    }

    private class FilterTestModule : AeroModuleBase, IFilterModule
    {
        public override string Name => "FilterTest";
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public override IReadOnlyList<string> Category => Array.Empty<string>();
        public override IReadOnlyList<string> Tags => Array.Empty<string>();
    }

    private class ContentDefinitionTestModule : AeroModuleBase, IContentDefinitionModule
    {
        public override string Name => "ContentDefinitionTest";
        public override string Version => "1.0.0";
        public override string Author => "Test";
        public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
        public override IReadOnlyList<string> Category => Array.Empty<string>();
        public override IReadOnlyList<string> Tags => Array.Empty<string>();
    }
}
