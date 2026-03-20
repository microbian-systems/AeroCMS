using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Grpc;

public class GrpcModule : AeroModuleBase
{
    public override string Name => nameof(GrpcModule);
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Communication"];
    public override IReadOnlyList<string> Tags => ["grpc", "api", "communication", "rpc"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = null, IHostEnvironment env = null)
    {
        services.AddMagicOnion();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMagicOnionService();
    }
}
