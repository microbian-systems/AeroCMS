using Aero.Cms.Core;
using Aero.Cms.Modules.Audit.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Aero.Cms.Abstractions.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Audit;

/// <summary>
/// Registers the audit-feed API and the scoped audit service implementation.
/// </summary>
/// <remarks>
/// The module has no declared module dependencies. Service registration occurs during
/// module configuration; endpoint registration occurs when the host runs the module.
/// </remarks>
[Module(nameof(AuditModule))]
public sealed class AuditModule : AeroWebModule
{
    /// <inheritdoc />
public override string Name => nameof(AuditModule);

    /// <inheritdoc />
public override string Version => AeroConstants.Version;

    /// <inheritdoc />
public override string Author => AeroConstants.Author;

    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];

    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["admin", "audit"];

    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["admin", "audit", "cms"];

    /// <summary>
    /// Registers <see cref="IAuditService"/> as a scoped <see cref="AuditService"/>.
    /// </summary>
    /// <param name="services">The application service collection to receive the scoped registration.</param>
    /// <param name="config">Unused module configuration.</param>
    /// <param name="env">Unused host environment.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IAuditService, AuditService>();
    }

    /// <summary>
    /// Adds the audit-feed endpoint to the host route builder.
    /// </summary>
    /// <param name="builder">The route builder that receives the audit endpoint group.</param>
    /// <returns>A task that is already complete after endpoint registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAuditApi();

        return Task.CompletedTask;
    }
}
