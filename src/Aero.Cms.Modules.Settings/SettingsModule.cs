using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Core;
using Aero.Cms.Core.Models;
using Aero.Cms.Modules.Settings.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Settings;

/// <summary>
/// Aero CMS Settings module - provides settings management functionality.
/// </summary>
[Module(nameof(SettingsModule))]
public sealed class SettingsModule : AeroWebModule, IConfigureAeroDB
{
        /// <inheritdoc />
public override string Name => nameof(SettingsModule);
        /// <inheritdoc />
public override string Version => AeroConstants.Version;
        /// <inheritdoc />
public override string Author => AeroConstants.Author;
        /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
        /// <inheritdoc />
public override IReadOnlyList<string> Category => ["admin", "settings"];
        /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["admin", "settings", "configuration", "management"];

        /// <summary>
    /// Registers the fixed-key Orleans settings grain as the settings actor contract.
    /// </summary>
    /// <param name="services">The service collection that receives the singleton grain proxy.</param>
    /// <param name="config">Module configuration; not used.</param>
    /// <param name="env">The host environment; not used.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroSettingActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroSettingActor>(0, "aero"));
    }

    /// <summary>Maps persisted settings to the stable plural table name.</summary>
    public void Configure(StoreOptions options)
    {
        options.Schema.For<Setting>()
            .TableName(Schemas.Tables.Settings)
            .Identity(setting => setting.Id);
    }

    /// <inheritdoc />
    public void Configure(IServiceProvider? services, StoreOptions options)
        => Configure(options);

        /// <summary>
    /// Adds the settings HTTP endpoints to the host route builder.
    /// </summary>
    /// <param name="builder">The endpoint route builder to mutate.</param>
    /// <returns>A task already completed after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapSettingsApi();
        return Task.CompletedTask;
    }
}
