using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Manager;

public class ManagerModule : AeroModuleBase
{
    public override string Name { get; } = nameof(ManagerModule);
    public override string Version { get; } = AeroConstants.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = [];
    public override IReadOnlyList<string> Tags { get; } = [];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
    }
}