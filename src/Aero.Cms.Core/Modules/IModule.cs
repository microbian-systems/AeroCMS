using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Modules;

/// <summary>
/// Defines the core contract for an Aero.Cms module.
/// </summary>
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }

    void ConfigureServices(IServiceCollection services);
    void Init(IEndpointRouteBuilder endpoints);
    void Configure(IModuleBuilder builder);
}

public interface IUiModule : IModule { }
public interface IApiModule : IModule { }
public interface IBackgroundModule : IModule { }
public interface IThemeAwareModule : IModule { }
public interface IContentDefinitionModule : IModule { }
