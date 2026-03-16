using Aero.Cms.Shared.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Modules.SimpleSecurity;

public class SimpleSecurityModule : IModule
{
    public string Name => "Aero.Cms.Security.Simple";
    public string Version => "1.0.0";
    public string Author => "AeroCMS";
    public IReadOnlyList<string> Dependencies => [];

    public void ConfigureServices(IServiceCollection services)
    {
    }

    public void Configure(IModuleBuilder builder)
    {
    }
}
