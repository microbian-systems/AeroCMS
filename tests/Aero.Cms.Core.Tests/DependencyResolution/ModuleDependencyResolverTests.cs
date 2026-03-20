using Aero.Cms.Core.Modules;
using Aero.Cms.Core.Tests.Services;
using Aero.Cms.Core.Tests.TestModules;
using FluentAssertions;

namespace Aero.Cms.Core.Tests.DependencyResolution;

/// <summary>
/// Tests for module dependency resolution and load ordering.
/// </summary>
public class ModuleDependencyResolverTests
{
    private readonly IModuleDependencyResolver _resolver;

    public ModuleDependencyResolverTests()
    {
        _resolver = new ModuleDependencyResolver();
    }

    [Test]
    public async Task ResolveAsync_WithNoDependencies_ShouldReturnSameOrder()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(SimpleTestModule)),
            CreateDescriptor(typeof(ApiTestModule))
        };

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.LoadOrder.Should().HaveCount(2);
        result.Modules.Should().HaveCount(2);
    }

    [Test]
    public async Task ResolveAsync_WithDependencies_ShouldOrderDependenciesFirst()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(DependentTestModule)), // depends on SimpleTest
            CreateDescriptor(typeof(SimpleTestModule))
        };

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.LoadOrder.Should().HaveCount(2);
        result.LoadOrder[0].Name.Should().Be("SimpleTest"); // Dependency first
        result.LoadOrder[1].Name.Should().Be("DependentTest");
    }

    [Test]
    public async Task ResolveAsync_WithMultipleDependencies_ShouldResolveCorrectly()
    {
        // Arrange - MultiDependency depends on SimpleTest and DependentTest
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(MultiDependencyTestModule)),
            CreateDescriptor(typeof(DependentTestModule)),
            CreateDescriptor(typeof(SimpleTestModule))
        };

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.LoadOrder.Should().HaveCount(3);

        // SimpleTest must come before DependentTest
        var simpleIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "SimpleTest");
        var dependentIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "DependentTest");
        simpleIndex.Should().BeLessThan(dependentIndex);

        // Both must come before MultiDependency
        var multiIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "MultiDependency");
        simpleIndex.Should().BeLessThan(multiIndex);
        dependentIndex.Should().BeLessThan(multiIndex);
    }

    [Test]
    public async Task ResolveAsync_WithMissingDependencies_ShouldThrowDependencyResolutionException()
    {
        // Arrange - MissingDependencyTestModule depends on NonExistentModule
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(MissingDependencyTestModule))
        };

        // Act
        var act = async () => await _resolver.ResolveAsync(modules);

        // Assert
        var ex = await act.Should().ThrowAsync<DependencyResolutionException>();
        ex.WithMessage("*missing dependencies*");
    }

    [Test]
    public async Task ResolveAsync_WithCircularDependencies_ShouldThrowDependencyResolutionException()
    {
        // Arrange - CircularA depends on CircularB, CircularB depends on CircularA
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(CircularATestModule)),
            CreateDescriptor(typeof(CircularBTestModule))
        };

        // Act
        var act = async () => await _resolver.ResolveAsync(modules);

        // Assert
        var ex = await act.Should().ThrowAsync<DependencyResolutionException>();
        ex.WithMessage("*Circular dependency detected*");
        ex.Which.CycleMembers.Should().NotBeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldIncludeCycleMembersInException()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(CircularATestModule)),
            CreateDescriptor(typeof(CircularBTestModule))
        };

        // Act & Assert
        try
        {
            await _resolver.ResolveAsync(modules);
            throw new InvalidOperationException("Expected exception was not thrown");
        }
        catch (DependencyResolutionException ex)
        {
            ex.CycleMembers.Should().NotBeNull();
            ex.CycleMembers.Should().Contain("CircularA");
            ex.CycleMembers.Should().Contain("CircularB");
        }
    }

    [Test]
    public async Task ResolveAsync_WithMissingDependencies_ShouldIncludeDetailsInException()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(MissingDependencyTestModule))
        };

        // Act & Assert
        try
        {
            await _resolver.ResolveAsync(modules);
            throw new InvalidOperationException("Expected exception was not thrown");
        }
        catch (DependencyResolutionException ex)
        {
            ex.OffendingModule.Should().Be("MissingDependency");
            ex.MissingDependencies.Should().Contain("NonExistentModule");
        }
    }

    [Test]
    public async Task ResolveAsync_WithEmptyModules_ShouldReturnEmptyGraph()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>();

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.LoadOrder.Should().BeEmpty();
        result.Modules.Should().BeEmpty();
    }

    [Test]
    public async Task ResolveAsync_ShouldPreserveModuleMetadata()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(SimpleTestModule))
        };

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.Modules.Should().ContainKey("SimpleTest");
        var module = result.Modules["SimpleTest"];
        module.Name.Should().Be("SimpleTest");
        module.Version.Should().Be("1.0.0");
        module.Author.Should().Be("TestAuthor");
    }

    [Test]
    public async Task ResolveAsync_ShouldProduceStableOrder()
    {
        // Arrange
        var modules = new List<ModuleDescriptor>
        {
            CreateDescriptor(typeof(DependentTestModule)),
            CreateDescriptor(typeof(SimpleTestModule))
        };

        // Act - run multiple times
        var results = new List<IReadOnlyList<ModuleDescriptor>>();
        for (int i = 0; i < 5; i++)
        {
            var result = await _resolver.ResolveAsync(modules);
            results.Add(result.LoadOrder);
        }

        // Assert - all results should have the same order
        var firstResult = results[0];
        foreach (var result in results.Skip(1))
        {
            result.Should().Equal(firstResult, (a, b) => a.Name == b.Name);
        }
    }

    [Test]
    public async Task ResolveAsync_ComplexDependencyGraph_ShouldResolveCorrectly()
    {
        // Arrange - create a diamond dependency pattern:
        //     A
        //    / \
        //   B   C
        //    \ /
        //     D
        var moduleA = new ModuleDescriptor
        {
            Name = "A",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(SimpleTestModule),
            Dependencies = Array.Empty<string>(),
            AssemblyName = "Test",
            IsUiModule = false
        };
        var moduleB = new ModuleDescriptor
        {
            Name = "B",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(SimpleTestModule),
            Dependencies = new[] { "A" },
            AssemblyName = "Test",
            IsUiModule = false
        };
        var moduleC = new ModuleDescriptor
        {
            Name = "C",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(SimpleTestModule),
            Dependencies = new[] { "A" },
            AssemblyName = "Test",
            IsUiModule = false
        };
        var moduleD = new ModuleDescriptor
        {
            Name = "D",
            Version = "1.0.0",
            Author = "Test",
            ModuleType = typeof(SimpleTestModule),
            Dependencies = new[] { "B", "C" },
            AssemblyName = "Test",
            IsUiModule = false
        };

        var modules = new List<ModuleDescriptor> { moduleD, moduleB, moduleC, moduleA };

        // Act
        var result = await _resolver.ResolveAsync(modules);

        // Assert
        result.LoadOrder.Should().HaveCount(4);

        var aIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "A");
        var bIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "B");
        var cIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "C");
        var dIndex = result.LoadOrder.ToList().FindIndex(m => m.Name == "D");

        // A must be first
        aIndex.Should().Be(0);

        // B and C must come after A but before D
        aIndex.Should().BeLessThan(bIndex);
        aIndex.Should().BeLessThan(cIndex);
        bIndex.Should().BeLessThan(dIndex);
        cIndex.Should().BeLessThan(dIndex);
    }

    private static ModuleDescriptor CreateDescriptor(Type moduleType)
    {
        var instance = (IAeroModule)Activator.CreateInstance(moduleType)!;

        return new ModuleDescriptor
        {
            Name = instance.Name,
            Version = instance.Version,
            Author = instance.Author,
            ModuleType = moduleType,
            Dependencies = instance.Dependencies,
            AssemblyName = moduleType.Assembly.GetName().Name ?? "Unknown",
            IsUiModule = typeof(IUiModule).IsAssignableFrom(moduleType),
            Order = instance.Order,
            Category = instance.Category,
            Tags = instance.Tags,
            DisabledInProduction = instance.DisabledInProduction
        };
    }
}
