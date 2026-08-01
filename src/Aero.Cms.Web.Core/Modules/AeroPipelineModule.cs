using Aero.Modular;
using Microsoft.AspNetCore.Builder;

namespace Aero.Cms.Web.Core.Modules;

/// <summary>
/// Defines a web module that contributes middleware to the ASP.NET Core request pipeline.
/// </summary>
public interface IAeroPipelineModule : IAeroModule
{
        /// <summary>
    /// Gets the middleware contribution order, defaulting to zero.
    /// </summary>
int PipelineOrder => 0;

        /// <summary>
    /// Adds this module's middleware to the supplied application builder.
    /// </summary>
    /// <param name="app">The host-owned pipeline builder.</param>
    /// <remarks>Registration is synchronous; ordering and the insertion point are controlled by the host integration.</remarks>
void ConfigurePipeline(IApplicationBuilder app);
}
