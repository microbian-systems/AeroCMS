using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Tenant;

/// <summary>
/// Registers tenant persistence metadata, repository access, and application services.
/// </summary>
[Module(nameof(TenantModule))]
public class TenantModule : AeroModuleBase, IConfigureAeroDB
{
    /// <inheritdoc />
public override string Name => nameof(TenantModule);
    /// <inheritdoc />
public override string Version => AeroConstants.Version;
    /// <inheritdoc />
public override string Author => AeroConstants.Author;
    /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
    /// <inheritdoc />
public override IReadOnlyList<string> Category => [];
    /// <inheritdoc />
public override IReadOnlyList<string> Tags => [];

    /// <summary>
    /// Registers <see cref="TenantModel"/> with the AeroDB document schema.
    /// </summary>
    /// <param name="opts">The store options to mutate.</param>
    public void Configure(StoreOptions opts)
    {
        opts.Schema.For<TenantModel>()
            .TableName(Schemas.Tables.Tenants);
    }

    /// <summary>
    /// Applies tenant store configuration through the provider-aware module contract.
    /// </summary>
    /// <param name="services">The host provider; this implementation does not use it.</param>
    /// <param name="opts">The store options to mutate.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Adds scoped tenant repository and service implementations after base module registration.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="config">Optional host configuration forwarded to the base module.</param>
    /// <param name="env">Optional host environment forwarded to the base module.</param>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();
    }
}
