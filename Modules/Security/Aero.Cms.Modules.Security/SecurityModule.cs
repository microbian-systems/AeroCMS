using Aero.Cms.Shared.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.Security;

public class SecurityModule : IModule
{
    public string Name => "Aero.Cms.Security.Identity";
    public string Version => "1.0.0";
    public string Author => "AeroCMS";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
        // EF Core Identity setup
    }

    public void Configure(IModuleBuilder builder)
    {
        // Admin UI registration
    }
}
