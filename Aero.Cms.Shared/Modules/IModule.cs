using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Shared.Modules;

/// <summary>
/// Defines the contract for an AeroCMS module.
/// </summary>
public interface IModule
{
    string Name { get; }
    string Version { get; }
    string Author { get; }
    IReadOnlyList<string> Dependencies { get; }

    void ConfigureServices(IServiceCollection services);
    void Configure(IModuleBuilder builder);
}
