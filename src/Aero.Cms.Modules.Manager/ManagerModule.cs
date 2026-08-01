using Aero.Cms.Core;
using Aero.Cms.Modules.Manager.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Manager;

/// <summary>
/// Registers the administrative dashboard endpoints.
/// </summary>
[Module(nameof(ManagerModule))]
public class ManagerModule : AeroWebModule
{
    /// <inheritdoc />
public override string Name { get; } = nameof(ManagerModule);
    /// <inheritdoc />
public override string Version { get; } = AeroConstants.Version;
    /// <inheritdoc />
public override string Author { get; } = AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category { get; } = [];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags { get; } = [];

    /// <summary>
    /// Applies base web-module service configuration without adding manager-specific services.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="config">Optional configuration forwarded to the base module.</param>
    /// <param name="env">Optional environment forwarded to the base module.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
    }

    /// <summary>
    /// Maps dashboard endpoints during module startup.
    /// </summary>
    /// <param name="builder">The host endpoint route builder.</param>
    /// <returns>A completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapDashboardApi();
        return Task.CompletedTask;
    }
}
