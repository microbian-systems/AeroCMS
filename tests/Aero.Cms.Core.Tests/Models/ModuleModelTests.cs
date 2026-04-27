using TUnit.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using FluentAssertions;

namespace Aero.Cms.Core.Tests.Models;

/// <summary>
/// Tests for ModuleDescriptor model.
/// </summary>
public class ModuleDescriptorTests
{
    [Test]
    public async Task ModuleDescriptor_Create_WithValidData_ShouldSucceed()
    {
        // Arrange & Act
        var descriptor = new ModuleDescriptor
        {
            Name = "TestModule",
            Version = "1.0.0",
            Author = "TestAuthor",
            ModuleType = typeof(object),
            Dependencies = new[] { "Dep1", "Dep2" },
            AssemblyName = "TestAssembly",
            PhysicalPath = "/path/to/assembly",
            IsUiModule = true
        };

        // Assert
        descriptor.Name.Should().Be("TestModule");
        descriptor.Version.Should().Be("1.0.0");
        descriptor.Author.Should().Be("TestAuthor");
        descriptor.ModuleType.Should().Be(typeof(object));
        descriptor.Dependencies.Should().HaveCount(2);
        descriptor.AssemblyName.Should().Be("TestAssembly");
        descriptor.PhysicalPath.Should().Be("/path/to/assembly");
        descriptor.IsUiModule.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task ModuleDescriptor_DefaultDependencies_ShouldBeEmpty()
    {
        // Arrange & Act
        var descriptor = new ModuleDescriptor
        {
            Name = "TestModule",
            Version = "1.0.0",
            Author = "TestAuthor",
            ModuleType = typeof(object),
            AssemblyName = "TestAssembly"
        };

        // Assert
        descriptor.Dependencies.Should().NotBeNull();
        descriptor.Dependencies.Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task ModuleDescriptor_PhysicalPath_ShouldBeNullable()
    {
        // Arrange & Act
        var descriptor = new ModuleDescriptor
        {
            Name = "TestModule",
            Version = "1.0.0",
            Author = "TestAuthor",
            ModuleType = typeof(object),
            AssemblyName = "TestAssembly",
            PhysicalPath = null
        };

        descriptor.PhysicalPath.Should().BeNull();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Tests for ModuleGraph model.
/// </summary>
public class ModuleGraphTests
{
    [Test]
    public async Task ModuleGraph_Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var descriptor = new ModuleDescriptor
        {
            Name = "TestModule",
            Version = "1.0.0",
            Author = "TestAuthor",
            ModuleType = typeof(object),
            AssemblyName = "TestAssembly"
        };

        var modules = new Dictionary<string, ModuleDescriptor> { ["TestModule"] = descriptor };
        var loadOrder = new List<ModuleDescriptor> { descriptor };

        // Act
        var graph = new ModuleGraph
        {
            Modules = modules,
            LoadOrder = loadOrder
        };

        graph.Modules.Should().HaveCount(1);
        graph.LoadOrder.Should().HaveCount(1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task ModuleGraph_Empty_ShouldReturnEmptyGraph()
    {
        // Act
        var graph = ModuleGraph.Empty();

        graph.Modules.Should().BeEmpty();
        graph.LoadOrder.Should().BeEmpty();
        await Task.CompletedTask;
    }

    [Test]
    public async Task ModuleGraph_LoadOrder_ShouldRespectOrdering()
    {
        // Arrange
        var moduleA = new ModuleDescriptor
        {
            Name = "A",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(object),
            AssemblyName = "Test"
        };
        var moduleB = new ModuleDescriptor
        {
            Name = "B",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(object),
            AssemblyName = "Test"
        };

        var modules = new Dictionary<string, ModuleDescriptor>
        {
            ["A"] = moduleA,
            ["B"] = moduleB
        };

        // Load order: B loads before A
        var loadOrder = new List<ModuleDescriptor> { moduleB, moduleA };

        // Act
        var graph = new ModuleGraph
        {
            Modules = modules,
            LoadOrder = loadOrder
        };

        graph.LoadOrder[0].Name.Should().Be("B");
        graph.LoadOrder[1].Name.Should().Be("A");
        await Task.CompletedTask;
}
}
