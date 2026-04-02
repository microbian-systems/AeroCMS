using Aero.Cms.Core;
using Aero.Cms.Core.Modules;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.SimpleSecurity;

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
