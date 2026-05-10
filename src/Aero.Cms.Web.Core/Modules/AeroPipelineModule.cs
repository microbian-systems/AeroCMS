using Aero.Modular;
using Microsoft.AspNetCore.Builder;

namespace Aero.Cms.Web.Core.Modules;

/// <summary>
/// Defines a web module that contributes middleware to the ASP.NET Core request pipeline.
/// </summary>
public interface IAeroPipelineModule : IAeroModule
{
    int PipelineOrder => 0;

    void ConfigurePipeline(IApplicationBuilder app);
}
