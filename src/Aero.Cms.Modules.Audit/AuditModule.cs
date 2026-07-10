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
/// Audit module - provides audit trail and activity feed functionality.
/// </summary>
[Module(nameof(AuditModule))]
public sealed class AuditModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(AuditModule);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["admin", "audit"];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["admin", "audit", "cms"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IAuditService, AuditService>();
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAuditApi();

        return Task.CompletedTask;
    }
}
