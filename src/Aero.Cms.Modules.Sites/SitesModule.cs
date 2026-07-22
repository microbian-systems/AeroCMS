using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Registers multi-site services, authorization policies, persistence schema, and admin endpoints.
/// </summary>
/// <remarks>
/// The module is ordered before other web modules so host-based site context can wrap the public
/// pipeline. Site-resolution middleware is omitted when the module is disabled in production.
/// </remarks>
[Module(nameof(SitesModule))]
public class SitesModule : AeroWebModule, IConfigureAeroDB
{
    /// <summary>
    /// Gets the stable module name used by module discovery.
    /// </summary>
public override string Name => nameof(SitesModule);

    /// <summary>
    /// Gets the AeroCMS version exposed for this module.
    /// </summary>
public override string Version => AeroConstants.Version;

    /// <summary>
    /// Gets the AeroCMS author metadata.
    /// </summary>
public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets the early execution order used to establish site context.
    /// </summary>
public override short Order => -9999;

    /// <summary>
    /// Gets the tenant and theme module dependencies that must be loaded first.
    /// </summary>
public override IReadOnlyList<string> Dependencies => ["TenantModule", "AeroThemeModule"];

    /// <summary>
    /// Gets the module categories used by discovery and administration.
    /// </summary>
public override IReadOnlyList<string> Category => ["multi-site", "website"];

    /// <summary>
    /// Gets the searchable tags describing the module.
    /// </summary>
public override IReadOnlyList<string> Tags => ["multi-site", "sites"];

    /// <summary>
    /// Registers site repositories, services, authorization policies, and optional startup middleware.
    /// </summary>
    /// <param name="services">The host service collection to mutate.</param>
    /// <param name="config">The optional host configuration forwarded to the base module.</param>
    /// <param name="env">The optional host environment forwarded to the base module.</param>
    /// <remarks>
    /// The four <c>site:*</c> policies use scoped assignment checks. When enabled, the startup filter
    /// is inserted at index zero so site resolution wraps later filters.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<ISiteLookupService, SiteLookupService>();
        services.AddScoped<IUserSiteService, UserSiteService>();
        services.AddScoped<ISiteStyleProfileResolver, SiteStyleProfileResolver>();
        services.AddScoped<ISiteStyleProfileService, SiteStyleProfileService>();
        services.AddScoped<ISiteThemeSelectionService, SiteThemeSelectionService>();

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
    /// Configures site, host, and user-assignment document indexes.
    /// </summary>
    /// <param name="opts">The AeroDB store options whose schema registrations are mutated.</param>
    /// <remarks>
    /// Site documents use optimistic concurrency. Host names are globally unique, while host-site
    /// and assignment user/site fields receive query indexes. A tenant foreign key is intentionally
    /// not configured.
    /// </remarks>
public void Configure(StoreOptions opts)
    {
        // SitesModel — no host info stored here; host resolution uses SiteHost.
        // DatabaseSchemaName/DocumentAlias not available in AeroDB
        var sites = opts.Schema.For<SitesModel>();
        sites.UseOptimisticConcurrency = true;
        sites.Index(x => x.IsEnabled);

        // SiteHost — separate document for multi-domain support.
        // Each row stores one normalized host/domain. The unique index on Host
        // prevents domain collisions across sites at the database level.
        // DatabaseSchemaName/DocumentAlias not available in AeroDB
        opts.Schema.For<SiteHost>().UniqueIndex(x => x.Host!);
        opts.Schema.For<SiteHost>().Index(x => x.SiteId);

        // UserSiteAssignment — maps users to sites with per-site permissions.
        // DatabaseSchemaName/DocumentAlias not available in AeroDB
        opts.Schema.For<UserSiteAssignment>().Index(x => x.UserId);
        opts.Schema.For<UserSiteAssignment>().Index(x => x.SiteId);

        // FK to TenantModel deferred — causes DDL ordering issue with embedded PG
        // opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);

        // base.Configure is not called — Configure<> above already adds
        // the standard entity indexes (CreatedBy, ModifiedBy, CreatedOn, ModifiedOn).
    }

    /// <summary>
    /// Applies the same schema configuration when invoked through the service-aware hook.
    /// </summary>
    /// <param name="services">The service provider supplied by the host; it is not consumed.</param>
    /// <param name="opts">The AeroDB store options to configure.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Maps the sites and client-error admin endpoints.
    /// </summary>
    /// <param name="endpoints">The route builder receiving the endpoint groups.</param>
    /// <returns>An already-completed task after synchronous route registration.</returns>
public override Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSitesApi();
        return Task.CompletedTask;
    }
}


