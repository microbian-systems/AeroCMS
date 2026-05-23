using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SimpleSecurity;

[Module(nameof(SimpleSecurityModule))]
public class SimpleSecurityModule : AeroModuleBase
{
    public override string Name => nameof(SimpleSecurityModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Security"];
    public override IReadOnlyList<string> Tags => ["security", "simple", "auth"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
    }

    public override void Configure(IAeroModuleBuilder builder)
    {
    }
}
