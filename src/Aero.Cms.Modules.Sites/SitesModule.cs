using Aero.Cms.Abstractions.Models;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Web.Core.Modules;
using Aero.Marten;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Sites;

public class SitesModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(SitesModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["mulit-site", "website"];
    public override IReadOnlyList<string> Tags => ["multi-site", "sites"];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        
        services.AddScoped<ISiteService, SiteService>();
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure<SitesModel>(services, opts);
        //opts.Schema.For<SitesModel>().IdStrategy(new SnowflakeIdGeneration()); // todo - add snowflake id generation strategy globally
        opts.Schema.For<SitesModel>().UniqueIndex(x => x.Name!);
        opts.Schema.For<SitesModel>().UniqueIndex(x => x.Hostname!);
        opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);
        opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);
    }
}


