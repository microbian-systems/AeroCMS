namespace Aero.Cms.Core.Modules;

/// <summary>
/// Defines the core contract for an Aero.Cms module.
/// </summary>
public interface IAeroModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }
    IReadOnlyList<string> Category { get; }
    IReadOnlyList<string> Tags { get; }
    void Configure(IModuleBuilder builder);
    void ConfigureServices(IServiceCollection services);
    void Run(IEndpointRouteBuilder endpoints);
    Task RunAsync(IEndpointRouteBuilder endpoints);
}

public interface IUiModule : IAeroModule { }
public interface IApiModule : IAeroModule { }
public interface IBackgroundModule : IAeroModule { }
public interface IThemeModule : IAeroModule { }
public interface IContentDefinitionModule : IAeroModule { }
