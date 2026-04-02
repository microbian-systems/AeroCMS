using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Core.Tests.TestModules;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ExcludeFromAssemblyDiscoveryAttribute : Attribute
{
}

/// <summary>
/// A concrete test module with no dependencies for basic testing.
/// </summary>
public class SimpleTestModule : AeroWebModule
{
    public override string Name => "SimpleTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => new[] { "test", "simple" };

    public bool ConfigureWasCalled { get; private set; }
    public bool ConfigureServicesWasCalled { get; private set; }
    public bool RunWasCalled { get; private set; }
    public IServiceCollection? ServicesReceived { get; private set; }

    public override void Configure(IAeroModuleBuilder builder)
    {
        ConfigureWasCalled = true;
        base.Configure(builder);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        ConfigureServicesWasCalled = true;
        ServicesReceived = services;
        base.ConfigureServices(services, config, env);
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
        RunWasCalled = true;
        base.Run(endpoints);
    }
}

/// <summary>
/// A test module that depends on SimpleTestModule.
/// </summary>
public class DependentTestModule : AeroModuleBase
{
    public override string Name => "DependentTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => new[] { "SimpleTest" };
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
    public override short Order => 10;
}

/// <summary>
/// A test module with multiple dependencies.
/// </summary>
public class MultiDependencyTestModule : AeroModuleBase
{
    public override string Name => "MultiDependency";
    public override string Version => "2.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => new[] { "SimpleTest", "DependentTest" };
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
    public override short Order => 20;
}

/// <summary>
/// A test module with a missing dependency (for testing error scenarios).
/// </summary>
public class MissingDependencyTestModule : AeroModuleBase
{
    public override string Name => "MissingDependency";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => new[] { "NonExistentModule" };
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// A test module that creates a circular dependency A -> B -> A.
/// </summary>
public class CircularATestModule : AeroModuleBase
{
    public override string Name => "CircularA";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => new[] { "CircularB" };
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// A test module that creates a circular dependency B -> A.
/// </summary>
public class CircularBTestModule : AeroModuleBase
{
    public override string Name => "CircularB";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => new[] { "CircularA" };
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// An abstract module that should be ignored during discovery.
/// </summary>
public abstract class AbstractTestModule : AeroModuleBase
{
    public override string Name => "AbstractTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// A generic module that should be ignored during discovery.
/// </summary>
public class GenericTestModule<T> : AeroModuleBase
{
    public override string Name => $"GenericTest_{typeof(T).Name}";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// A UI module for testing specialized interface detection.
/// </summary>
public class UiTestModule : AeroModuleBase, IUiModule
{
    public override string Name => "UiTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "UI" };
    public override IReadOnlyList<string> Tags => new[] { "ui" };
}

/// <summary>
/// An API module for testing specialized interface detection.
/// </summary>
public class ApiTestModule : AeroModuleBase, IApiModule
{
    public override string Name => "ApiTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "API" };
    public override IReadOnlyList<string> Tags => new[] { "api" };
}

/// <summary>
/// A background module for testing specialized interface detection.
/// </summary>
public class BackgroundTestModule : AeroModuleBase, IBackgroundModule
{
    public override string Name => "BackgroundTest";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Background" };
    public override IReadOnlyList<string> Tags => new[] { "background" };
}

/// <summary>
/// A module with the same name as SimpleTestModule (for duplicate name testing).
/// </summary>
[ExcludeFromAssemblyDiscovery]
public class DuplicateNameTestModule : AeroModuleBase
{
    public override string Name => "SimpleTest"; // Same name as SimpleTestModule
    public override string Version => "2.0.0";
    public override string Author => "AnotherAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
}

/// <summary>
/// Module with negative order (should load before default order 0).
/// </summary>
public class EarlyLoadingTestModule : AeroModuleBase
{
    public override string Name => "EarlyLoading";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
    public override short Order => -100;
}

/// <summary>
/// Module with high order (should load after default order 0).
/// </summary>
public class LateLoadingTestModule : AeroModuleBase
{
    public override string Name => "LateLoading";
    public override string Version => "1.0.0";
    public override string Author => "TestAuthor";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
    public override IReadOnlyList<string> Category => new[] { "Test" };
    public override IReadOnlyList<string> Tags => Array.Empty<string>();
    public override short Order => 1000;
}
