using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Grpc;

public class GrpcModule : ModuleBase
{
    public override string Name => "gRPC Server";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMagicOnion();
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMagicOnionService();
    }
}
