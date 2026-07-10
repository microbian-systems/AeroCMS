using Aero.Cms.Core;
using Aero.Cms.Web.Core.Pipelines;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Analytics;

/// <summary>
/// Represents a class for AnalyticsModule.
/// </summary>
[Module(nameof(AnalyticsModule))]
public class AnalyticsModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(AnalyticsModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["Marketing", "Tracking"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["analytics", "tracking", "metrics"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddOptions<AnalyticsSettings>().BindConfiguration("AeroCms:Analytics");
        services.AddScoped<ISeoScriptRenderer, SeoScriptRenderer>();
        services.AddScoped<IPageReadHook, AnalyticsInjectionHook>();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IAeroModuleBuilder builder)
    {
        // No specific builder registration needed for basic read hook if it's resolved via DI
    }
}
