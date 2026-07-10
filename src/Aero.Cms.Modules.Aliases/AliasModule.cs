using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Abstractions.Validators;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Aero.Cms.Web.Core.Pipelines;
using Aero.Modular;
using FluentValidation;
using AeroDB.Sable;
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
///   AliasDocument → AeroDB persistence (owner)
///   IAliasRuleCache → ImmutableDictionary hot lookup (zero DB per request)
///   AliasRewriteRule → sync IRule, reads cache only, site-scoped via IAeroSiteSlice
///   AliasStartupFilter → IStartupFilter, registers UseRewriter
///   AliasRuleCacheWarmupService → BackgroundService, loads cache on startup
///
/// Pipeline order: SitesModule (-9999) → AliasModule (-9998) → rest of pipeline
/// </summary>
[Module(nameof(AliasModule))]
public class AliasModule : AeroWebModule, IConfigureAeroDB
{
    /// <summary>Load after SitesModule (-9999) so site is resolved before alias lookup.</summary>
    public override short Order => -9998;

        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(AliasModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => [];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// ConfigureServices method.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        // Core alias services
        services.AddScoped<IAliasRepository, AliasRepository>();
        services.AddScoped<IAliasService, AliasService>();

        // Grain-backed alias service (Orleans actor) — service wrapper
        services.AddScoped<IAeroAliasService, AeroAliasService>();

        // Grain interface — direct injection for thin API controllers
        services.AddSingleton<IAeroAliasActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroAliasActor>(0, "aero"));

        // FluentValidation validators — input validation in the API layer
        services.AddScoped<IValidator<CreateAliasRequest>, CreateAliasRequestValidator>();
        services.AddScoped<IValidator<DeleteAliasRequest>, DeleteAliasRequestValidator>();

        services.AddScoped<IPageSaveHook, SlugRewriteHook>();

        // In-memory alias cache — zero DB I/O per request
        services.AddMemoryCache();
        services.AddSingleton<IAliasRuleCache, AliasRuleCache>();

        // Rewrite rule (IRule) — consumes cache only, no DB access on hot path
        services.AddSingleton<AliasRewriteRule>();

        // Background warmup — loads cache from AeroDB on startup
        services.AddHostedService<AliasRuleCacheWarmupService>();

        // Pipeline middleware — runs UseRewriter AFTER SiteResolutionMiddleware.
        // Insert(0) guarantees this filter wraps the pipeline after SitesModule's filter.
        if (!DisabledInProduction)
        {
            services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, AliasStartupFilter>());
        }
    }

        /// <summary>
    /// RunAsync method.
    /// </summary>
public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAliasesApi();
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(StoreOptions opts)
    {
        // DocumentAlias not available in AeroDB
        opts.Schema.For<AliasDocument>().Identity(x => x.Id);
        opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
        opts.Schema.For<AliasDocument>().UniqueIndex(x => new { x.SiteId, x.OldPath }); // site-scoped composite unique
        opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
        opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
    }

        /// <summary>
    /// Configure method.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }
}



