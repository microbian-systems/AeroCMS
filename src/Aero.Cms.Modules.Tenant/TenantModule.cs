using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Web.Core.Modules;
using Marten;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Modules.Tenant;

public class TenantModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(TenantModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<TenantModel>().DocumentAlias(Schemas.Tables.Tenants);
        
        base.Configure(services, opts);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantService, TenantService>();
    }
}
