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
    public override string Name => nameof(AuditModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["admin", "audit"];

    public override IReadOnlyList<string> Tags => ["admin", "audit", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddScoped<IAuditService, AuditService>();
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAuditApi();

        return Task.CompletedTask;
    }
}
