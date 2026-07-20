using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Abstractions.Validators;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Modules.Aliases.Areas.Api.v1;
using Aero.Cms.Services;
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
/// Registers alias persistence, the alias cache, page-route alias staging, and
/// the administrative endpoint mappings. Its order is intended to follow the
/// sites module so that requests have site context before alias rewriting;
/// final middleware ordering remains the host's responsibility.
/// </summary>
[Module(nameof(AliasModule))]
public class AliasModule : AeroWebModule, IConfigureAeroDB
{
    /// <summary>Gets the module order intended to follow site resolution.</summary>
    public override short Order => -9998;

    /// <summary>Gets the module's stable registration name.</summary>
public override string Name => nameof(AliasModule);
        /// <summary>
    /// Gets the CMS version associated with this module.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets the CMS author metadata associated with this module.
    /// </summary>
public override string Author => AeroConstants.Author;
        /// <summary>
    /// Gets the explicitly declared module dependencies; this module declares none.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets the module categories; this module declares none.
    /// </summary>
public override IReadOnlyList<string> Category => [];
        /// <summary>
    /// Gets the module tags; this module declares none.
    /// </summary>
public override IReadOnlyList<string> Tags => [];

        /// <summary>
    /// Registers alias services, validation, cache infrastructure, and the
    /// rewrite startup filter when it is enabled for the current environment.
    /// </summary>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        // Core alias services
        services.AddScoped<IAliasRepository, AliasRepository>();
        services.AddScoped<IAliasService, AliasService>();
        services.AddScoped<IPageRouteAliasWriter, PageRouteAliasWriter>();

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
    /// Maps the module's administrative alias endpoints.
    /// </summary>
public override async Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapAliasesApi();
    }

        /// <summary>
    /// Configures the persisted alias document schema, including uniqueness of
    /// the site, culture, and normalized old-path scope.
    /// </summary>
public void Configure(StoreOptions opts)
    {
        // DocumentAlias not available in AeroDB
        opts.Schema.For<AliasDocument>().Identity(x => x.Id);
        opts.Schema.For<AliasDocument>().Index(x => x.SiteId);
        opts.Schema.For<AliasDocument>().Index(x => x.Culture);
        opts.Schema.For<AliasDocument>().Index(x => x.OwnerId);
        opts.Schema.For<AliasDocument>()
            .UniqueIndex(x => new { x.SiteId, x.Culture, x.NormalizedOldPath });
        opts.Schema.For<AliasDocument>().Index(x => x.NewPath);
        opts.Schema.For<AliasDocument>().Index(x => x.CreatedOn);
        opts.Schema.For<AliasDocument>().Index(x => x.ModifiedOn);
    }

        /// <summary>
    /// Applies this module's document-schema configuration using the supplied
    /// service provider only to satisfy the configuration contract.
    /// </summary>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }
}



