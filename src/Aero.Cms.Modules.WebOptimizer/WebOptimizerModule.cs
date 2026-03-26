using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.WebOptimizer;

public class WebOptimizerModule : AeroModuleBase
{
    public override string Name { get; } = nameof(WebOptimizerModule);
    public override string Version { get; } = AeroVersion.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = ["utilities", "web"];
    public override IReadOnlyList<string> Tags { get; } = ["utilities", "web"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration config=null, IHostEnvironment env=null)
    {
        // todo - configure WebOptimizer more granularly w/ bundles, etc
        // https://weboptimizer.azurewebsites.net/
        // https://www.nuget.org/packages/LigerShark.WebOptimizer.Core/
        var minifyIfProduction = env.IsProduction();
        services.AddWebOptimizer(minifyJavaScript: minifyIfProduction, minifyCss: minifyIfProduction);
    }
}