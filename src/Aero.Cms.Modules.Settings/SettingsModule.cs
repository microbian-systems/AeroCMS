using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Core;
using Aero.Cms.Modules.Settings.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Settings;

/// <summary>
/// Aero CMS Settings module - provides settings management functionality.
/// </summary>
[Module(nameof(SettingsModule))]
public sealed class SettingsModule : AeroWebModule
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SettingsModule);
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
public override IReadOnlyList<string> Category => ["admin", "settings"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["admin", "settings", "configuration", "management"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroSettingActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroSettingActor>(0, "aero"));
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSettingsApi();
        return Task.CompletedTask;
    }
}
