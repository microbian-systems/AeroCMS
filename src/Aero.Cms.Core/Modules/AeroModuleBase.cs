using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Aero.Cms.Core.Modules;

/// <summary>
/// A base class for Aero.Cms modules that provides default implementations.
/// </summary>
public abstract class AeroModuleBase : IAeroModule, IDisposable
{
    protected readonly ILogger log = Log.Logger;

    /// <inheritdoc/>
    public abstract string Name { get; }
    /// <inheritdoc/>
    public abstract string Version { get; }
    /// <inheritdoc/>
    public abstract string Author { get; }
    /// <inheritdoc/>
    public virtual short Order { get; } = 0;
    /// <inheritdoc/>
    public virtual Dictionary<string, Uri> Urls { get; } = [];
    /// <inheritdoc/>
    public abstract IReadOnlyList<string> Dependencies { get; }
    /// <inheritdoc/>
    public abstract IReadOnlyList<string> Category { get; }
    /// <inheritdoc/>
    public abstract IReadOnlyList<string> Tags { get; }
    /// <inheritdoc/>
    public virtual bool DisabledInProduction => false;
    /// <inheritdoc/>>
    public virtual bool DisabledInProductions { get; set; }
    /// <inheritdoc/>
    public virtual string? Description => null;
    /// <inheritdoc/>
    public bool Disabled { get ; set ; }

    /// <inheritdoc/>
    public virtual void Configure(IModuleBuilder builder)
    {
    }

    /// <inheritdoc/>
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
    }

    /// <inheritdoc/>
    public virtual void Run(IEndpointRouteBuilder endpoints) => RunAsync(endpoints).GetAwaiter().GetResult();

    /// <inheritdoc/>
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
