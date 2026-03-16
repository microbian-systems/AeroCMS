using Aero.Cms.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Grpc;

public class GrpcModule : AeroModuleBase
{
    public override string Name => nameof(GrpcModule);

    public override string Version => "1.0.0";

    public override string Author => "Microbian Systems";

    public override IReadOnlyList<string> Dependencies => [];

    public override string Description => "Grpc server for Aero CMS";


    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
    {
        base.ConfigureServices(services, config);

        services.AddMagicOnion();
    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        app.MapMagicOnionService();
    }
}