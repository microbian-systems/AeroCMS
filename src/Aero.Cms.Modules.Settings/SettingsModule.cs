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
    public override string Name => nameof(SettingsModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["admin", "settings"];
    public override IReadOnlyList<string> Tags => ["admin", "settings", "configuration", "management"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroSettingActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroSettingActor>(0, "aero"));
    }

    public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSettingsApi();
        return Task.CompletedTask;
    }
}
