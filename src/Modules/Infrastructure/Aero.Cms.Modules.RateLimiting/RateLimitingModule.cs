using Aero.Cms.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.RateLimiting;

public class RateLimitingModule : ModuleBase
{
    public override string Name => "RateLimiting";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];

    public override void ConfigureServices(IServiceCollection services)
    {
        // todo - enable database config to supply the type of rate limiting (sliding window, fixed, etc)
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("Global", opt =>
            {
                opt.Window = TimeSpan.FromSeconds(1);
                opt.PermitLimit = 100;
                opt.QueueLimit = 0;
            });
        });
    }

    public override void Init(IEndpointRouteBuilder endpoints)
    {
    }

    public override void Configure(IModuleBuilder builder)
    {
    }
}
