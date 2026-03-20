using Aero.Cms.Core.Modules;
using Aero.Cms.Core.Tests.Services;
using Aero.Cms.Core.Tests.TestModules;
using FluentAssertions;
using System.Reflection;

namespace Aero.Cms.Core.Tests.Discovery;

/// <summary>
/// Tests for the module discovery service.
/// </summary>
public class ModuleDiscoveryTests
{
    private readonly Aero.Cms.Core.Tests.Services.IModuleDiscoveryService _discoveryService;

    public ModuleDiscoveryTests()
    {
        _discoveryService = new Aero.Cms.Core.Tests.Services.ModuleDiscoveryService();
    }

    [Test]
    public async Task DiscoverAsync_WithValidModules_ShouldReturnAllConcreteModules()
    {
        // Arrange - use current assembly which contains test modules
        var assemblies = new[] { typeof(SimpleTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(0);

        // Verify specific test modules are discovered
        result.Should().Contain(m => m.Name == "SimpleTest");
        result.Should().Contain(m => m.Name == "DependentTest");
        result.Should().Contain(m => m.Name == "MultiDependency");
    }

    [Test]
    public async Task DiscoverAsync_ShouldIgnoreAbstractModules()
    {
        // Arrange
        var assemblies = new[] { typeof(AbstractTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        result.Should().NotContain(m => m.Name == "AbstractTest");
    }

    [Test]
    public async Task DiscoverAsync_ShouldIgnoreGenericModules()
    {
        // Arrange
        var assemblies = new[] { typeof(GenericTestModule<>).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        result.Should().NotContain(m => m.Name.StartsWith("GenericTest"));
    }

    [Test]
    public async Task DiscoverAsync_WithDuplicateNames_ShouldThrowInvalidOperationException()
    {
        var moduleTypes = new[] { typeof(SimpleTestModule), typeof(DuplicateNameTestModule) };

        var act = async () => await _discoveryService.DiscoverFromTypesAsync(moduleTypes);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate module name 'SimpleTest'*");
    }

    [Test]
    public async Task DiscoverAsync_ShouldCorrectlyIdentifyUiModules()
    {
        // Arrange
        var assemblies = new[] { typeof(UiTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        var uiModule = result.Should().ContainSingle(m => m.Name == "UiTest").Subject;
        uiModule.IsUiModule.Should().BeTrue();
    }

    [Test]
    public async Task DiscoverAsync_ShouldCorrectlyIdentifyNonUiModules()
    {
        // Arrange
        var assemblies = new[] { typeof(SimpleTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        var simpleModule = result.Should().ContainSingle(m => m.Name == "SimpleTest").Subject;
        simpleModule.IsUiModule.Should().BeFalse();
    }

    [Test]
    public async Task DiscoverAsync_ShouldPopulateAllDescriptorFields()
    {
        // Arrange
        var assemblies = new[] { typeof(SimpleTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        var module = result.Should().ContainSingle(m => m.Name == "SimpleTest").Subject;
        module.Name.Should().Be("SimpleTest");
        module.Version.Should().Be("1.0.0");
        module.Author.Should().Be("TestAuthor");
        module.ModuleType.Should().Be(typeof(SimpleTestModule));
        module.AssemblyName.Should().NotBeNullOrEmpty();
        module.Dependencies.Should().NotBeNull();
    }

    [Test]
    public async Task DiscoverAsync_WithNoAssemblies_ShouldUseAppDomainAssemblies()
    {
        var result = await _discoveryService.DiscoverAsync(null);

        result.Should().NotBeNull();
        result.Should().Contain(m => m.Name == "SimpleTest");
    }

    [Test]
    public async Task DiscoverAsync_ShouldIncludeModuleDependencies()
    {
        // Arrange
        var assemblies = new[] { typeof(DependentTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        var dependent = result.Should().ContainSingle(m => m.Name == "DependentTest").Subject;
        dependent.Dependencies.Should().Contain("SimpleTest");
    }

    [Test]
    public async Task DiscoverAsync_ShouldHandleEmptyAssemblies()
    {
        // Arrange
        var emptyAssembly = System.Reflection.Assembly.GetExecutingAssembly();
        var assemblies = Array.Empty<System.Reflection.Assembly>();

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task DiscoverAsync_ShouldIncludePhysicalPath()
    {
        // Arrange
        var assemblies = new[] { typeof(SimpleTestModule).Assembly };

        // Act
        var result = await _discoveryService.DiscoverAsync(assemblies);

        // Assert
        var module = result.Should().ContainSingle(m => m.Name == "SimpleTest").Subject;
        module.PhysicalPath.Should().NotBeNullOrEmpty();
    }
}
