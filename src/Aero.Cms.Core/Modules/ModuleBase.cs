using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Modules;

/// <summary>
/// A base class for Aero.Cms modules that provides default implementations.
/// </summary>
public abstract class AeroModuleBase : IModule, IDisposable
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Author { get; }
    public virtual IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void Init(IEndpointRouteBuilder endpoints)
    {
    }

    public virtual void Configure(IModuleBuilder builder)
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Release managed resources here if needed
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
