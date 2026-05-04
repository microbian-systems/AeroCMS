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
///   AliasRewriteRule → sync IRule, reads cache only
///   AliasPipelineStartupFilter → IStartupFilter, auto-registers UseRewriter + 404 handler
///   AliasRuleCacheWarmupService → BackgroundService, loads cache on startup
/// </summary>
[Module(nameof(AliasModule))]
public class AliasModule : AeroWebModule
{
    /// <summary>Load early so ConfigureServices runs before other modules.</summary>
    public override short Order => -9999;

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

        // Rewrite rule (IRule) — consumes cache only, no DB access
        services.AddSingleton<AliasRewriteRule>();

        // Background warmup — loads cache from Marten on startup
        services.AddHostedService<AliasRuleCacheWarmupService>();

        // Pipeline middleware — runs UseRewriter + UseStatusCodePages BEFORE all other middleware.
        // Insert(0) guarantees our IStartupFilter wraps the entire request pipeline.
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, AliasStartupFilter>());
    }

    public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
            
    }

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
}
