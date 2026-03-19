namespace Aero.Cms.Core.Modules;

/// <summary>
/// A base class for Aero.Cms modules that provides default implementations.
/// </summary>
public abstract class AeroModuleBase : IAeroModule, IDisposable
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Author { get; }
    public abstract IReadOnlyList<string> Dependencies { get; }
    public abstract IReadOnlyList<string> Category { get; }
    public abstract IReadOnlyList<string> Tags { get; }

    public virtual void Configure(IModuleBuilder builder)
    {
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void Run(IEndpointRouteBuilder endpoints)
    {
    }

    public virtual Task RunAsync(IEndpointRouteBuilder builder) => Task.CompletedTask;



    // todo - impl IAsyncDisposable pattern for modules
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
