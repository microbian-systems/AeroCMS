using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Core.Pipelines;
using Aero.Modular;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Aliases;

/// <summary>
/// Site alias management module for handling URL aliases and redirects.
///
/// Architecture:
///   AliasDocument → Marten persistence (owner)
///   IAliasRuleCache → ImmutableDictionary hot lookup (zero DB per request)
///   AliasRewriteRule → sync IRule, reads cache only, site-scoped via IAeroSiteSlice
///   AliasStartupFilter → IStartupFilter, registers UseRewriter
///   AliasRuleCacheWarmupService → BackgroundService, loads cache on startup
///
/// Pipeline order: SitesModule (-9999) → AliasModule (-9998) → rest of pipeline
/// </summary>
[Module(nameof(AliasModule))]
public class AliasModule : AeroWebModule
{
    /// <summary>Load after SitesModule (-9999) so site is resolved before alias lookup.</summary>
    public override short Order => -9998;

    public override string Name => nameof(AliasModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => [];
    public override IReadOnlyList<string> Tags => [];

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        // Core alias services
        services.AddScoped<IAliasRepository, AliasRepository>();
        services.AddScoped<IAliasService, AliasService>();
        services.AddScoped<IPageSaveHook, SlugRewriteHook>();

        // In-memory alias cache — zero DB I/O per request
        services.AddMemoryCache();
        services.AddSingleton<IAliasRuleCache, AliasRuleCache>();

        // Rewrite rule (IRule) — consumes cache only, no DB access on hot path
        services.AddSingleton<AliasRewriteRule>();

        // Background warmup — loads cache from Marten on startup
        services.AddHostedService<AliasRuleCacheWarmupService>();

        // Pipeline middleware — runs UseRewriter AFTER SiteResolutionMiddleware.
        // Insert(0) guarantees this filter wraps the pipeline after SitesModule's filter.
        if (!DisabledInProduction)
        {
            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, AliasStartupFilter>());
        }
    }

    public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
            
    }

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<AliasDocument>().DocumentAlias(Schemas.Tables.Aliases);
        opts.Schema.For<AliasDocument>().Identity(x => x.Id);
        opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
        opts.Schema.For<AliasDocument>().UniqueIndex(x => x.SiteId, x => x.OldPath); // site-scoped composite unique
        opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
        opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
    }
}
