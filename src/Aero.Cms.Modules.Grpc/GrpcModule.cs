using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Grpc;

public class GrpcModule : AeroModuleBase
{
    public override string Name => nameof(GrpcModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Communication"];
    public override IReadOnlyList<string> Tags => ["grpc", "api", "communication", "rpc"];

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMagicOnion();
    }

    public override void Run(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMagicOnionService();
    }
}
