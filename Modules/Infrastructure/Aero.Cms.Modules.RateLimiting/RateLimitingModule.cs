using Aero.Cms.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.RateLimiting;

public class RateLimitingModule : AeroModuleBase
{
    public override string Name=> "RateLimiting";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => [];
    public override string Description => "Rate limiter for AeroCMS";

    public override void ConfigureServices(IServiceCollection services, IConfiguration config = default)
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

    public override void Init(IServiceProvider sp)
    {
        
    }

    public override void Run(IEndpointRouteBuilder app)
    {
        
    }

    public override Task RunAsync(IEndpointRouteBuilder app)
    {
        return Task.CompletedTask;
    }


    public override void Configure(IAeroModuleBuilder builder)
    {
    }
}
