using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aero.Cms.Modules.MiniProfiler;

public class MiniProfilerModule : AeroWebModule, IStartupFilter
{
    public override string Name => nameof(MiniProfilerModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["profiler", "performance"];

    public override IReadOnlyList<string> Tags => ["profiler", "performance"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        var enabled = config?.GetValue<bool>("Aero:Modules:MiniProfiler:Enable") ?? false;
        if (enabled)
        {
            services.AddMiniProfiler(options =>
            {
                options.RouteBasePath = "/_profiler";
                options.PopupRenderPosition = StackExchange.Profiling.RenderPosition.BottomLeft;
                options.PopupShowTimeWithChildren = true;
            }).AddEntityFramework();
        }
        
        base.ConfigureServices(services, config, env);
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var enabled = app.ApplicationServices.GetService<IConfiguration>()?
                .GetValue<bool>("Aero:Modules:MiniProfiler:Enable") ?? false;
            if (enabled)
            {
                app.UseMiniProfiler();
            }

            next(app);
        };
    }
}
