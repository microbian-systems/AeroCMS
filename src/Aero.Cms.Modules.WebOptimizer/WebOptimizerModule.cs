using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.WebOptimizer;

/// <summary>
/// Represents a class for WebOptimizerModule.
/// </summary>
[Module(nameof(WebOptimizerModule))]
public class WebOptimizerModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name { get; } = nameof(WebOptimizerModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version { get; } = AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author { get; } = AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies { get; } = [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category { get; } = ["utilities", "web"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags { get; } = ["utilities", "web"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration config=null, IHostEnvironment env=null)
    {
        // todo - configure WebOptimizer more granularly w/ bundles, etc
        // https://weboptimizer.azurewebsites.net/
        // https://www.nuget.org/packages/LigerShark.WebOptimizer.Core/
        var minifyIfProduction = env.IsProduction();
        services.AddWebOptimizer(minifyJavaScript: minifyIfProduction, minifyCss: minifyIfProduction);
    }
}