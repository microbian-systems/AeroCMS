using Aero.Cms.Core;
using Aero.Cms.Modules.Modules.Services;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Modules;

/// <summary>
/// Aero CMS Modules management module.
/// </summary>
[Module(nameof(ModulesModule))]
public sealed class ModulesModule : AeroModuleBase, IConfigureAeroDB
{
    public override string Name => nameof(ModulesModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Settings", "Management"];
    public override IReadOnlyList<string> Tags => ["modules", "settings", "configuration", "management"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        // Register the initialization service
        services.TryAddScoped<IModuleInitializationService, ModuleInitializationService>();
        
        // Ensure the core module state store is registered
        services.TryAddScoped<IModuleStateStore, ModuleStateStore>();
    }

    /// <summary>
    /// Configures the ModuleDocument schema — implements <see cref="IConfigureAeroDB.Configure(StoreOptions)"/>.
    /// </summary>
    public void Configure(StoreOptions opts)
    {
        opts.Schema.For<ModuleDocument>().Identity(x => x.Id);
        opts.Schema.For<ModuleDocument>().UniqueIndex(x => x.Name);
        opts.Schema.For<ModuleDocument>().Index(x => x.Category);
        opts.Schema.For<ModuleDocument>().Index(x => x.Disabled);
        opts.Schema.For<ModuleDocument>().Index(x => x.DisabledInProduction);
        opts.Schema.For<ModuleDocument>().Index(x => x.Order);
    }

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
        // Configure<ModuleDocument>(services, opts); — generic Configure<T> removed from AeroModuleBase, use Schema directly
    }
}
