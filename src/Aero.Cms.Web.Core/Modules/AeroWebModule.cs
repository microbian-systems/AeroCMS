using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    void Run(IEndpointRouteBuilder builder);
    Task RunAsync(IEndpointRouteBuilder builder);
}

/// <summary>
/// Provides a base class for web modules that can be registered with an endpoint routing builder.
/// </summary>
/// <remarks>Inherit from this class to define custom web modules that participate in endpoint routing. Override
/// the RunAsync method to configure endpoints or perform additional setup during application startup.</remarks>
public abstract class AeroWebModule : AeroModuleBase, IAeroWebModule
{
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

