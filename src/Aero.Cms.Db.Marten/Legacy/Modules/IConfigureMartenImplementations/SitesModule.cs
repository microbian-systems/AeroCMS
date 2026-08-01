using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Represents a class for SitesModule.
/// </summary>
[Module(nameof(SitesModule))]
public class SitesModule : AeroWebModule, IConfigureMarten
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(SitesModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public override short Order => -9999;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => ["TenantModule"];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["multi-site", "website"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["multi-site", "sites"];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<ISiteLookupService, SiteLookupService>();
        services.AddScoped<IUserSiteService, UserSiteService>();

        // Register site authorization handler and policies.
        // Policies: site:create, site:read, site:update, site:delete
        // Usage: [Authorize(Policy = "site:read")] on endpoints or pages.
        services.AddScoped<IAuthorizationHandler, SitePermissionHandler>();
        services.Configure<AuthorizationOptions>(options =>
        {
            options.AddPolicy("site:read",   policy => policy.AddRequirements(new SitePermissionRequirement("read")));
            options.AddPolicy("site:create", policy => policy.AddRequirements(new SitePermissionRequirement("create")));
            options.AddPolicy("site:update", policy => policy.AddRequirements(new SitePermissionRequirement("update")));
            options.AddPolicy("site:delete", policy => policy.AddRequirements(new SitePermissionRequirement("delete")));
        });

        // Register startup filter for site resolution middleware.
        // Runs first in pipeline because SitesModule has the lowest Order (-9999)
        // and ConfigureServices is called in load order.
        if (!DisabledInProduction)
        {
            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, SiteStartupFilter>());
        }
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // SitesModel — no host info stored here; host resolution uses SiteHost.
        opts.Schema.For<SitesModel>().DatabaseSchemaName(Schemas.Database);
        opts.Schema.For<SitesModel>().DocumentAlias(Schemas.Tables.Sites);
        Configure<SitesModel>(services, opts);
        opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);

        // SiteHost — separate document for multi-domain support.
        // Each row stores one normalized host/domain. The unique index on Host
        // prevents domain collisions across sites at the database level.
        opts.Schema.For<SiteHost>().DatabaseSchemaName(Schemas.Database);
        opts.Schema.For<SiteHost>().DocumentAlias(Schemas.Tables.SiteHosts);
        Configure<SiteHost>(services, opts);
        opts.Schema.For<SiteHost>().UniqueIndex(x => x.Host!);
        opts.Schema.For<SiteHost>().Index(x => x.SiteId);

        // UserSiteAssignment — maps users to sites with per-site permissions.
        opts.Schema.For<UserSiteAssignment>().DatabaseSchemaName(Schemas.Database);
        opts.Schema.For<UserSiteAssignment>().DocumentAlias(Schemas.Tables.SitePerms);
        Configure<UserSiteAssignment>(services, opts);
        opts.Schema.For<UserSiteAssignment>().Index(x => x.UserId);
        opts.Schema.For<UserSiteAssignment>().Index(x => x.SiteId);

        // FK to TenantModel deferred — causes DDL ordering issue with embedded PG
        // opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);

        // base.Configure is not called — Configure<> above already adds
        // the standard entity indexes (CreatedBy, ModifiedBy, CreatedOn, ModifiedOn).
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSitesApi();
        return Task.CompletedTask;
    }
}


