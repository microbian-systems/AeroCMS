using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Tenant;

/// <summary>
/// Represents a class for TenantModule.
/// </summary>
[Module(nameof(TenantModule))]
public class TenantModule : AeroModuleBase, IConfigureMarten
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(TenantModule);
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
public override IReadOnlyList<string> Category => [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<TenantModel>().DocumentAlias(Schemas.Tables.Tenants);
        
        base.Configure(services, opts);
    }

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();
    }
}
