using System;
using System.Collections.Generic;
using System.Text;
using Aero.Cms.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.WebOptimizer;

public class WebOptimizerModule : AeroModuleBase
{
    public override string Name { get; }
    public override string Version { get; }
    public override string Author { get; }
    public override IReadOnlyList<string> Dependencies { get; }
    public override IReadOnlyList<string> Category { get; }
    public override IReadOnlyList<string> Tags { get; }

    public override void ConfigureServices(IServiceCollection services, IConfiguration config=null, IHostEnvironment env=null)
    {
        // todo - configure WebOptimizer more granularly w/ bundles, etc
        // https://weboptimizer.azurewebsites.net/
        // https://www.nuget.org/packages/LigerShark.WebOptimizer.Core/
        var minifyIfProduction = env.IsProduction();
        services.AddWebOptimizer(minifyJavaScript: minifyIfProduction, minifyCss: minifyIfProduction);
    }
}