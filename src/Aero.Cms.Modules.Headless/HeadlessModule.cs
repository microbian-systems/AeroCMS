using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Aero.Modular;

namespace Aero.Cms.Modules.Headless;

/// <summary>
/// Aero CMS Headless module — manages OpenAPI/Scalar documentation.
/// All API endpoints (21 groups) have been migrated to domain modules.
/// PreviewBlockFragment moved to ManagerModule (Phase 4).
/// </summary>
[Module(nameof(HeadlessModule))]
public sealed class HeadlessModule : AeroWebModule
{
    public override string Name => nameof(HeadlessModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["admin", "management"];

    public override IReadOnlyList<string> Tags => ["admin", "management", "cms"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        return Task.CompletedTask;
    }
}
