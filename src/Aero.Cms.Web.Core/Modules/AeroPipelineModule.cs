using Aero.Modular;
using Microsoft.AspNetCore.Builder;

namespace Aero.Cms.Web.Core.Modules;

/// <summary>
/// Defines a web module that contributes middleware to the ASP.NET Core request pipeline.
/// </summary>
public interface IAeroPipelineModule : IAeroModule
{
        /// <summary>
    /// Gets or sets the Pipeline Order.
    /// </summary>
int PipelineOrder => 0;

        /// <summary>
    /// ConfigurePipeline method.
    /// </summary>
void ConfigurePipeline(IApplicationBuilder app);
}
