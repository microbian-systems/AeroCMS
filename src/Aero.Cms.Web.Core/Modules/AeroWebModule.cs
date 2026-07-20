using Microsoft.AspNetCore.Routing;
using Aero.Modular;

namespace Aero.Cms.Web.Core.Modules;


/// <summary>
/// Defines a contract for configuring and running a web module within an ASP.NET Core application.
/// </summary>
/// <remarks>Implement this interface to register endpoints, middleware, or other web components using the
/// provided endpoint route builder. The interface is typically used to modularize application features and organize
/// endpoint configuration logic.</remarks>
public interface IAeroWebModule : IAeroModule
{
    /// <summary>Configures the module synchronously against an endpoint route builder.</summary>
    /// <param name="builder">The host-owned route builder.</param>
    /// <remarks>Implementations may map endpoints or perform startup work; authentication and policies are module-defined.</remarks>
void Run(IEndpointRouteBuilder builder);
    /// <summary>Configures the module asynchronously against an endpoint route builder.</summary>
    /// <param name="builder">The host-owned route builder.</param>
    /// <returns>A task representing module startup configuration.</returns>
Task RunAsync(IEndpointRouteBuilder builder);
}

/// <summary>
/// Provides a base class for web modules that can be registered with an endpoint routing builder.
/// </summary>
/// <remarks>Inherit from this class to define custom web modules that participate in endpoint routing. Override
/// the RunAsync method to configure endpoints or perform additional setup during application startup.</remarks>
public abstract class AeroWebModule : AeroModuleBase, IAeroWebModule
{
    /// <summary>
    /// Runs <see cref="RunAsync"/> synchronously and propagates its exception.
    /// </summary>
    /// <param name="builder">The host-owned route builder.</param>
    /// <remarks>Blocking asynchronous implementations can deadlock when invoked under a custom synchronization context.</remarks>
    public virtual void Run(IEndpointRouteBuilder builder)
        => RunAsync(builder).GetAwaiter().GetResult();

    /// <summary>
    /// Configures endpoint routing for the application asynchronously.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to configure application endpoints. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual Task RunAsync(IEndpointRouteBuilder builder)
    {
        return Task.CompletedTask;
    }
}

