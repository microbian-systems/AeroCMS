using Aero.Cms.Modules.Docs.Areas.Api.v1;
using Aero.Cms.Web.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Aero.Cms.Core;
using Aero.Modular;
using Aero.Cms.Abstractions.Actors;
using Aero.Core.Http;
using Wolverine;
using ZiggyCreatures.Caching.Fusion;

namespace Aero.Cms.Modules.Docs;

/// <summary>
/// Registers the documentation module's schema, services, localized Razor routes, actor, and admin API.
/// </summary>
/// <remarks>
/// The mapped admin route group does not add an authorization policy itself. The host must protect
/// the routes and establish a trustworthy <see cref="ISiteContext"/>.
/// </remarks>
[Module(nameof(DocsModule))]
public sealed class DocsModule : AeroWebModule, IConfigureAeroDB
{
    /// <summary>
    /// Gets the stable module name used by module discovery.
    /// </summary>
public override string Name => nameof(DocsModule);

    /// <summary>
    /// Gets the Aero package version.
    /// </summary>
public override string Version =>AeroConstants.Version;

    /// <summary>
    /// Gets the Aero package author.
    /// </summary>
public override string Author => AeroConstants.Author;

    /// <summary>
    /// Gets the module startup order.
    /// </summary>
public override short Order => 100;

    /// <summary>
    /// Gets the empty list of declared module dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

    /// <summary>
    /// Gets the categories used to classify the module.
    /// </summary>
public override IReadOnlyList<string> Category => ["documentation", "knowledge base"];

    /// <summary>
    /// Gets the discovery tags associated with the module.
    /// </summary>
public override IReadOnlyList<string> Tags => ["docs", "markdown", "kbase"];


    /// <summary>
    /// Configures optimistic concurrency and query indexes for documentation pages.
    /// </summary>
    /// <param name="opts">The mutable document-store configuration.</param>
    /// <remarks>
    /// The schema enforces uniqueness for the combination of site, culture, and slug.
    /// </remarks>
public void Configure(StoreOptions opts)
    {
        var docs = opts.Schema.For<DocsPage>()
            .TableName(Schemas.Tables.Docs);
        docs.UseOptimisticConcurrency = true;
        docs.Index(x => x.SiteId);
        docs.UniqueIndex(x => new { x.SiteId, x.Culture, x.Slug });
        docs.Index(x => x.Culture);
        docs.Index(x => x.TranslationGroupId);
        docs.Index(x => x.ParentId);
        docs.Index(x => x.Order);
        docs.Index(x => x.PublishedOn);
        docs.Index(x => x.CreatedOn);
        docs.Index(x => x.ModifiedOn);

        // Full-text search (Phase 1)
        // NgramIndex not available in AeroDB

    }

    /// <summary>
    /// Applies the document schema configuration when a service provider is available.
    /// </summary>
    /// <param name="services">The service provider; this implementation does not use it.</param>
    /// <param name="opts">The mutable document-store configuration.</param>
public void Configure(IServiceProvider services, StoreOptions opts)
    {
        Configure(opts);
    }

    /// <summary>
    /// Registers localized public routes, scoped content and tree services, and the docs grain proxy.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="config">The host configuration; this implementation does not use it.</param>
    /// <param name="env">The host environment; this implementation does not use it.</param>
    /// <remarks>
    /// The content-service factory captures the current site and optional authenticated user name.
    /// When no user identity is available, writes are audited as <c>system</c>.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        services.Configure<RazorPagesOptions>(options =>
        {
            options.Conventions.AddAreaPageRoute("Docs", "/DocsIndex", "/{culture}/docs");
            options.Conventions.AddAreaPageRoute("Docs", "/Doc", "/{culture}/docs/{*slug}");
            options.Conventions.AddAreaPageRoute("Docs", "/Doc", "/_cms/preview/docs/drafts/{draftId:long}");
            options.Conventions.AddAreaPageRouteModelConvention("Docs", "/Doc", model =>
            {
                foreach (var selector in model.Selectors)
                {
                    var template = selector.AttributeRouteModel?.Template;
                    if (string.Equals(
                            template?.TrimStart('/'),
                            "_cms/preview/docs/drafts/{draftId:long}",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selector.EndpointMetadata.Add(new AuthorizeAttribute("site:read"));
                    }
                }
            });
        });

        // Content service — factory resolves ISiteContext + IHttpContextAccessor
        // at the boundary and converts them to explicit primitives so the service
        // never touches HTTP transport concerns.
        services.AddScoped<IDocsService>(sp =>
        {
            var session = sp.GetRequiredService<IDocumentSession>();
            var bus = sp.GetRequiredService<IMessageBus>();
            var siteContext = sp.GetRequiredService<ISiteContext>();
            var logger = sp.GetRequiredService<ILogger<DocsContentService>>();
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var cache = sp.GetService<IFusionCache>();
            var actor = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            return new DocsContentService(session, bus, siteContext, logger, actor, cache);
        });
        services.AddScoped<IDocsTreeService, DocsTreeService>();

        // Grain-backed actor — direct injection for thin API controllers
        services.AddSingleton<IAeroDocsActor>(sp =>
            sp.GetRequiredService<IGrainFactory>().GetGrain<IAeroDocsActor>(0, "aero"));
    }

    /// <summary>
    /// Maps the documentation admin endpoints onto the host route builder.
    /// </summary>
    /// <param name="builder">The endpoint route builder receiving the routes.</param>
    /// <returns>An already-completed task after route registration.</returns>
    /// <remarks>No authorization requirement is attached by this method.</remarks>
public override Task RunAsync(IEndpointRouteBuilder builder)
    {
        builder.MapDocsApi();
        return Task.CompletedTask;
    }
}



