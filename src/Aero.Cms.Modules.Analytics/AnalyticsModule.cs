using Aero.Cms.Core;
using Aero.Cms.Web.Core.Pipelines;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Analytics;

/// <summary>
/// Registers server-side components that emit configured third-party analytics markup.
/// </summary>
/// <remarks>
/// The module does not ingest, aggregate, persist, or query analytics events. It has no
/// declared module dependencies and relies on the host pipeline to execute registered page-read hooks.
/// </remarks>
[Module(nameof(AnalyticsModule))]
public class AnalyticsModule : AeroModuleBase
{
    /// <inheritdoc />
public override string Name => nameof(AnalyticsModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => ["Marketing", "Tracking"];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["analytics", "tracking", "metrics"];

    /// <summary>
    /// Binds analytics settings from <c>AeroCms:Analytics</c> and registers the script renderer
    /// and page-read hook as scoped services.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">Unused module configuration.</param>
    /// <param name="env">Unused host environment.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.AddOptions<AnalyticsSettings>().BindConfiguration("AeroCms:Analytics");
        services.AddScoped<ISeoScriptRenderer, SeoScriptRenderer>();
        services.AddScoped<IPageReadHook, AnalyticsInjectionHook>();
    }

    /// <summary>
    /// Performs no module-builder registrations; the read hook is discovered through dependency injection.
    /// </summary>
    /// <param name="builder">The module builder, which this implementation does not modify.</param>
public override void Configure(IAeroModuleBuilder builder)
    {
        // No specific builder registration needed for basic read hook if it's resolved via DI
    }
}
