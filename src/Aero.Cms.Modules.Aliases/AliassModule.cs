using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Pipelines;
using Aero.Modular;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Site alias management module for handling URL aliases and redirects.
/// </summary>
public class AliasModule : AeroModuleBase
{
    public override string Name => nameof(AliasModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<AliasDocument>().DocumentAlias(Schemas.Tables.Aliases);
        opts.Schema.For<AliasDocument>().Identity(x => x.Id);
        opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
        opts.Schema.For<AliasDocument>().UniqueIndex(x => x.OldPath);
        opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
        opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddScoped<IAliasRepository, AliasRepository>();
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();
    }
}