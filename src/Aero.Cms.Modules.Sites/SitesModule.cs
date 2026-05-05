using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Modules;
using Aero.Marten;
using Aero.Modular;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Sites;

[Module(nameof(SitesModule))]
public class SitesModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(SitesModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override short Order => -9999;
    public override IReadOnlyList<string> Dependencies => ["TenantModule"];
    public override IReadOnlyList<string> Category => ["multi-site", "website"];
    public override IReadOnlyList<string> Tags => ["multi-site", "sites"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<ISiteLookupService, SiteLookupService>();

        // Register startup filter for site resolution middleware.
        // Runs first in pipeline because SitesModule has the lowest Order (-9999)
        // and ConfigureServices is called in load order.
        if (!DisabledInProduction)
        {
            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, SiteStartupFilter>());
        }
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure<SitesModel>(services, opts);
        opts.Schema.For<SitesModel>().UniqueIndex(x => x.PrimaryHost!);
        opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);
        // FK to TenantModel deferred — causes DDL ordering issue with embedded PG
        // opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);
        // Hosts is stored in the JSONB document body. Marten's JSONB containment
        // operators handle Contains queries natively without a flat duplicate column.

        // base.Configure is not called — Configure<SitesModel> above already adds
        // the standard entity indexes (CreatedBy, ModifiedBy, CreatedOn, ModifiedOn).
    }
}


