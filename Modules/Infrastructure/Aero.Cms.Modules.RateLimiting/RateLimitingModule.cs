using Aero.Cms.Shared.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.RateLimiting;

public class RateLimitingModule : IModule
{
    public string Name => "Aero.Cms.Infrastructure.RateLimiting";
    public string Version => "1.0.0";
    public string Author => "AeroCMS";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
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

    public void Configure(IModuleBuilder builder)
    {
    }
}
