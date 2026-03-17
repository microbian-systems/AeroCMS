using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core;

public abstract class AeroModuleBase : IAeroModule, IDisposable
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Author { get; }
    public abstract IReadOnlyList<string> Dependencies { get; }
    public abstract string Description { get; }
    public abstract bool Enabled { get; set; }
    public abstract bool AllowInProduction { get; set; } 
    public abstract IReadOnlyList<string> Categories { get; }
    public abstract IReadOnlyList<string> Tags { get; }

    public virtual void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        
    }

    public virtual void Init(IServiceProvider sp)
    {
        
    }

    public virtual Task InitAsync(IServiceProvider sp)
    {
        return Task.CompletedTask;
    }

    public virtual void Run(IEndpointRouteBuilder app)
    {
        
    }

    public virtual Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }

    public virtual void Configure(IAeroModuleBuilder builder)
    {
        
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // TODO release managed resources here
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}