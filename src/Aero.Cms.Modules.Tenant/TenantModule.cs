using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using AeroDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aero.Modular;

namespace Aero.Cms.Modules.Tenant;

[Module(nameof(TenantModule))]
public class TenantModule : AeroModuleBase, IConfigureAeroDB
{
    public override string Name => nameof(TenantModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public void Configure(StoreOptions opts)
    {
        // DocumentAlias not available in AeroDB
    }

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();
    }
}
