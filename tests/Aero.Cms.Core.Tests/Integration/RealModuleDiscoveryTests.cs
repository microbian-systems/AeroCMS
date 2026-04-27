using TUnit.Core;
using Aero.Cms.Modules.Setup;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Cache;
using Aero.Cms.Modules.Security;
using Aero.Cms.Modules.SimpleSecurity;
using Aero.Cms.Modules.RateLimiting;
using Aero.Cms.Modules.Analytics;
using Aero.Cms.Web.Core.Modules;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Aero.Cms.Core.Extensions;
using Aero.Cms.Modules.Modules.Services;
using Aero.Modular;

namespace Aero.Cms.Core.Tests.Integration;

/// <summary>
/// End-to-end integration tests for module discovery with real project modules.
/// Verifies Track 01 module system works with actual module implementations.
/// </summary>
public class RealModuleDiscoveryTests
{
    private readonly ModuleDiscoveryService _discoveryService;
    private readonly IHostEnvironment _hostEnvironment;

    public RealModuleDiscoveryTests()
    {
        _hostEnvironment = Substitute.For<IHostEnvironment>();
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var options = Options.Create(new ModuleDiscoveryOptions
        {
            ScanApplicationDependencies = false,
            IncludeDisabledInProduction = true,
            ExcludedAssemblyPatterns = new[]
            {
                "System.*",
                "Microsoft.*",
                "netstandard",
                "mscorlib"
            }
        });

        _discoveryService = new ModuleDiscoveryService(options, _hostEnvironment, NullLogger<ModuleDiscoveryService>.Instance);
    }

    [Test]
    public async Task DiscoverAsync_WithApplicationDependencies_ShouldDiscoverReferencedModules()
    {
        var options = Options.Create(new ModuleDiscoveryOptions
        {
            ScanApplicationDependencies = true,
            IncludeDisabledInProduction = true,
            TypeFilter = type => type.Namespace == null || !type.Namespace.StartsWith("Aero.Cms.Core.Tests", StringComparison.Ordinal),
            ExcludedAssemblyPatterns = new[]
            {
                "System.*",
                "Microsoft.*",
                "netstandard",
                "mscorlib"
            }
        });

        var discoveryService = new ModuleDiscoveryService(options, _hostEnvironment, NullLogger<ModuleDiscoveryService>.Instance);

        var result = await discoveryService.DiscoverAsync();

        result.Should().NotBeEmpty();
        result.Select(module => module.Name).Should().Contain(new[]
        {
            "TestModule",
            "SetupModule",
            "IdentityModule",
            "CacheModule",
            "Security",
            "SimpleSecurityModule",
            "RewriteModule",
            "RateLimitingModule",
            "AnalyticsModule"
        });
    }

    [Test]
    public async Task AddAeroModulesAsync_ShouldRegisterDiscoveredModulesIntoServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleDiscovery:ScanApplicationDependencies"] = "true",
                ["ModuleDiscovery:IncludeDisabledInProduction"] = "true",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:0"] = "System.*",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:1"] = "Microsoft.*",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:2"] = "netstandard",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:3"] = "mscorlib",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:4"] = "Aero.Cms.Core.Tests*"
            })
            .Build();

        await services.AddAeroModulesAsync(configuration, _hostEnvironment);

        await using var provider = services.BuildServiceProvider();
        var modules = provider.GetServices<IAeroModule>().ToList();
        var moduleTypeNames = modules.Select(module => module.GetType().Name).OrderBy(name => name).ToList();

        modules.Count.Should().BeGreaterThanOrEqualTo(9);
        moduleTypeNames.Should().Contain(new[]
        {
            "AnalyticsModule",
            "CacheModule",
            "IdentityModule",
            "RateLimitingModule",
            "RewriteModule",
            "SecurityModule",
            "SetupModule",
            "SimpleSecurityModule",
            "TestModule"
        });
    }

    [Test]
    public async Task AddAeroModulesAsync_ShouldResolveSetupModuleFirstByOrder()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ModuleDiscovery:ScanApplicationDependencies"] = "true",
                ["ModuleDiscovery:IncludeDisabledInProduction"] = "true",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:0"] = "System.*",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:1"] = "Microsoft.*",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:2"] = "netstandard",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:3"] = "mscorlib",
                ["ModuleDiscovery:ExcludedAssemblyPatterns:4"] = "Aero.Cms.Core.Tests*"
            })
            .Build();

        await services.AddAeroModulesAsync(configuration, _hostEnvironment);

        await using var provider = services.BuildServiceProvider();
        var orderedModules = provider.GetServices<IAeroModule>()
            .OrderBy(module => module.Order)
            .ToList();

        orderedModules.Should().NotBeEmpty();
        orderedModules.First().Name.Should().Be("SetupModule");
        orderedModules.First().Order.Should().Be(-32768);
    }

    [Test]
    public async Task DiscoverFromTypesAsync_WithRealModules_ShouldDiscoverAllModules()
    {
        // Arrange - Collect all real module types from actual project assemblies
        var realModuleTypes = new List<Type>
        {
            typeof(SetupModule),
            typeof(IdentityModule),
            typeof(CacheBusterModule),
            typeof(SecurityModule),
            typeof(SimpleSecurityModule),
            typeof(RateLimitingModule),
            typeof(AnalyticsModule)
        };

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(realModuleTypes);

        // Assert - Verification
        Console.WriteLine();
        Console.WriteLine("=== REAL MODULE DISCOVERY VERIFICATION ===");
        Console.WriteLine($"Expected Modules: {realModuleTypes.Count}");
        Console.WriteLine($"Discovered Modules: {result.Count}");
        Console.WriteLine();
        Console.WriteLine("Discovered Module Details:");
        Console.WriteLine(new string('-', 60));

        foreach (var descriptor in result.OrderBy(m => m.Name))
        {
            Console.WriteLine($"  - {descriptor.Name}");
            Console.WriteLine($"      Version: {descriptor.Version}");
            Console.WriteLine($"      Assembly: {descriptor.AssemblyName}");
            Console.WriteLine($"      Categories: [{string.Join(", ", descriptor.Category)}]");
            Console.WriteLine($"      Tags: [{string.Join(", ", descriptor.Tags)}]");
            Console.WriteLine($"      Is UI Module: {descriptor.IsUiModule}");
            Console.WriteLine($"      Order: {descriptor.Order}");
            Console.WriteLine($"      DisabledInProduction: {descriptor.DisabledInProduction}");
            Console.WriteLine(new string('-', 60));
        }

        Console.WriteLine();
        Console.WriteLine("=== VERIFICATION SUMMARY ===");
        
        // Basic assertions
        result.Should().NotBeNull();
        result.Should().HaveCount(realModuleTypes.Count, 
            "all real project modules should be discovered");
        
        // Verify all expected modules are present
        var moduleNames = result.Select(m => m.Name).ToList();
        
        moduleNames.Should().Contain("TestModule");
        moduleNames.Should().Contain("SetupModule");
        moduleNames.Should().Contain("IdentityModule");
        moduleNames.Should().Contain("CacheModule");
        moduleNames.Should().Contain("Security");
        moduleNames.Should().Contain("SimpleSecurityModule");
        moduleNames.Should().Contain("RewriteModule");
        moduleNames.Should().Contain("RateLimitingModule");
        moduleNames.Should().Contain("AnalyticsModule");

        Console.WriteLine($"RESULT: PASS - All {result.Count} real modules discovered successfully!");
        Console.WriteLine("==========================================");
        Console.WriteLine();
    }

    [Test]
    public async Task DiscoverFromTypesAsync_WithRealModules_ShouldPopulateAllDescriptorFields()
    {
        // Arrange
        var cacheModuleType = typeof(CacheBusterModule);

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(
            new[] {  cacheModuleType });

        // Assert
        var cacheModule = result.Should().ContainSingle(m => m.Name == "CacheModule").Subject;
        cacheModule.Category.Should().Contain("Infrastructure");
        cacheModule.Tags.Should().Contain("cache");
    }

    [Test]
    public async Task DiscoverFromTypesAsync_WithRealModules_ShouldIdentifyModuleTypes()
    {
        // Arrange
        var moduleTypes = new[]
        {
            typeof(AnalyticsModule)
        };

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(moduleTypes);

        // Assert
        var testModule = result.First(m => m.Name == "TestModule");
        var analyticsModule = result.First(m => m.Name == "AnalyticsModule");

        // Verify module metadata is correctly extracted
        testModule.ModuleType.FullName.Should().Contain("TestModule");
        analyticsModule.ModuleType.FullName.Should().Contain("AnalyticsModule");
        
        // Verify assemblies are correctly identified
        testModule.AssemblyName.Should().Contain("Testing");
        analyticsModule.AssemblyName.Should().Contain("Analytics");
    }

    [Test]
    public async Task DiscoverFromTypesAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(Array.Empty<Type>());

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public async Task DiscoverFromTypesAsync_ShouldHandleModuleMetadata()
    {
        // Arrange
        var identityModuleType = typeof(IdentityModule);
        var rateLimitingModuleType = typeof(RateLimitingModule);

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(
            new[] { identityModuleType, rateLimitingModuleType });

        // Assert
        result.Should().HaveCount(2);
        var identityModule = result.First(m => m.Name == "IdentityModule");
        identityModule.Category.Should().Contain("Identity");
        identityModule.Category.Should().Contain("Security");
        identityModule.Tags.Should().Contain("auth");
        identityModule.Tags.Should().Contain("identity");
        identityModule.Tags.Should().Contain("users");

        var rateLimitingModule = result.First(m => m.Name == "RateLimitingModule");
        rateLimitingModule.Category.Should().Contain("Security");
        rateLimitingModule.Category.Should().Contain("Infrastructure");
        rateLimitingModule.Tags.Should().Contain("ratelimit");
    }

    [Test]
    public async Task DiscoverFromTypesAsync_ShouldPreserveModuleDependencies()
    {
        // Arrange
        var moduleTypes = new[] { typeof(SetupModule) };

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(moduleTypes);

        // Assert
        var setupModule = result.Should().ContainSingle(m => m.Name == "SetupModule").Subject;
        setupModule.Dependencies.Should().NotBeNull();
        setupModule.Dependencies.Should().BeEmpty();
    }

    [Test]
    public async Task DiscoverFromTypesAsync_WithInvalidTypes_ShouldSkipNonModuleTypes()
    {
        // Arrange - Include a non-module type
        var moduleTypes = new List<Type>
        {
            typeof(string), // Not a module
            typeof(ModuleDiscoveryOptions) // Not a module
        };

        // Act
        var result = await _discoveryService.DiscoverFromTypesAsync(moduleTypes);

        // Assert - Only valid modules should be returned
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("TestModule");
    }

    [Test]
    public async Task ModuleDiscoveryService_Integration_ShouldWorkEndToEnd()
    {
        // This is the primary end-to-end verification test for Track 01
        
        // Arrange - All known real modules
        var allKnownModules = new[]
        {
            typeof(SetupModule),
            typeof(IdentityModule),
            typeof(CacheBusterModule),
            typeof(SecurityModule),
            typeof(SimpleSecurityModule),
            typeof(RateLimitingModule),
            typeof(AnalyticsModule)
        };

        // Act
        var discoveredModules = await _discoveryService.DiscoverFromTypesAsync(allKnownModules);

        // Assert & Report
        Console.WriteLine();
        Console.WriteLine("==========================================");
        Console.WriteLine("  TRACK 01 MODULE SYSTEM VERIFICATION");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        
        var pass = true;
        var errorMessages = new List<string>();

        // Verify module count
        if (discoveredModules.Count != allKnownModules.Length)
        {
            pass = false;
            errorMessages.Add("Module count mismatch: expected " + allKnownModules.Length + ", got " + discoveredModules.Count);
        }

        // Verify each expected module was discovered
        foreach (var expectedType in allKnownModules)
        {
            var expectedName = expectedType == typeof(SecurityModule)
                ? "Security"
                : expectedType.Name;

            var found = discoveredModules.FirstOrDefault(m => m.Name == expectedName);
            
            if (found == null)
            {
                pass = false;
                errorMessages.Add("Module not found: " + expectedName);
            }
            else
            {
                Console.WriteLine("[PASS] " + expectedName);
            }
        }

        Console.WriteLine();
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("Total Modules Discovered: " + discoveredModules.Count + "/" + allKnownModules.Length);
        Console.WriteLine("----------------------------------------");
        Console.WriteLine();
        
        if (pass)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("  RESULT: PASS - Track 01 Module System Working");
            Console.WriteLine("==========================================");
        }
        else
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("  RESULT: FAIL - Track 01 Module System Issues");
            Console.WriteLine("==========================================");
            foreach (var error in errorMessages)
            {
                Console.WriteLine("  ERROR: " + error);
            }
        }
        
        Console.WriteLine();

        // Final assertion
        pass.Should().BeTrue();
        discoveredModules.Should().HaveCount(allKnownModules.Length);
}
}
