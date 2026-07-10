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
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(ModulesModule);
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
public override IReadOnlyList<string> Category => ["Infrastructure", "Settings", "Management"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["modules", "settings", "configuration", "management"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
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

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
        // Configure<ModuleDocument>(services, opts); — generic Configure<T> removed from AeroModuleBase, use Schema directly
    }
}
