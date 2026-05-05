using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Data.Repositories;
using Aero.Cms.Web.Core.Modules;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Marten;
using Aero.Modular;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Modules.Sites;

[Module(nameof(SitesModule))]
public class SitesModule : AeroWebModule, IConfigureMarten
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

    public override void Configure(IServiceProvider services, StoreOptions opts)
    {
        // SitesModel — no host info stored here; host resolution uses SiteHost.
        Configure<SitesModel>(services, opts);
        opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);

        // SiteHost — separate document for multi-domain support.
        // Each row stores one normalized host/domain. The unique index on Host
        // prevents domain collisions across sites at the database level.
        Configure<SiteHost>(services, opts);
        opts.Schema.For<SiteHost>().UniqueIndex(x => x.Host!);
        opts.Schema.For<SiteHost>().Index(x => x.SiteId);

        // UserSiteAssignment — maps users to sites with per-site permissions.
        Configure<UserSiteAssignment>(services, opts);
        opts.Schema.For<UserSiteAssignment>().Index(x => x.UserId);
        opts.Schema.For<UserSiteAssignment>().Index(x => x.SiteId);

        // FK to TenantModel deferred — causes DDL ordering issue with embedded PG
        // opts.Schema.For<SitesModel>().ForeignKey<TenantModel>(x => x.TenantId);

        // base.Configure is not called — Configure<> above already adds
        // the standard entity indexes (CreatedBy, ModifiedBy, CreatedOn, ModifiedOn).
    }

    public override Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        var sitesPath = $"/{HttpConstants.ApiPrefix}admin/sites";
        var group = endpoints.MapGroup(sitesPath).WithTags("Sites");

        // ── Current site (cookie-based selection) ──

        // GET /api/v1/admin/sites/current — returns the currently selected site from cookie
        group.MapGet("/current", async (
            HttpContext httpContext,
            ISiteLookupService siteLookup) =>
        {
            var cookie = httpContext.Request.Cookies["AeroCms.SiteId"];
            if (string.IsNullOrEmpty(cookie) || !long.TryParse(cookie, out var siteId))
                return Results.Ok(null);

            var allSites = await siteLookup.GetAllAsync();
            var site = allSites.FirstOrDefault(s => s.Id == siteId);
            return Results.Ok(site);
        });

        // POST /api/v1/admin/sites/current — sets the current site (writes cookie)
        group.MapPost("/current", (
            HttpContext httpContext,
            long siteId) =>
        {
            httpContext.Response.Cookies.Append("AeroCms.SiteId", siteId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                IsEssential = true,
                Secure = true
            });
            return Results.Ok();
        });

        // DELETE /api/v1/admin/sites/current — clears the current site selection
        group.MapDelete("/current", (HttpContext httpContext) =>
        {
            httpContext.Response.Cookies.Delete("AeroCms.SiteId", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
            return Results.Ok();
        });

        // ── Site CRUD (admin-only delegated to ISiteService) ──

        // GET /api/v1/admin/sites — list all sites
        group.MapGet("/", async (ISiteService siteService) =>
        {
            var result = await siteService.GetAllSitesAsync();
            return result switch
            {
                Result<IEnumerable<SitesModel>, AeroError>.Ok ok => Results.Ok(ok.Value),
                Result<IEnumerable<SitesModel>, AeroError>.Failure f => Results.Problem(f.Error.ToString()),
                _ => Results.Problem("Unexpected result")
            };
        });

        // GET /api/v1/admin/sites/{id} — get site by ID
        group.MapGet("/{id:long}", async (long id, ISiteService siteService) =>
        {
            var site = await siteService.GetSiteByIdAsync(id);
            return site switch
            {
                Option<SitesModel>.Some s => Results.Ok(s.Value),
                _ => Results.NotFound()
            };
        });

        // POST /api/v1/admin/sites — create site
        group.MapPost("/", async (CreateSiteRequest request, ISiteService siteService) =>
        {
            var validator = new Abstractions.Validators.SiteRequestValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
                return Results.ValidationProblem(validationResult.ToDictionary());

            var site = new SitesModel
            {
                Id = Snowflake.NewId(),
                TenantId = 1, // default tenant — tenants managed externally
                Name = request.Name,
                Description = request.Description,
                IsEnabled = true,
                DefaultCulture = "en-US"
            };

            var createResult = await siteService.CreateSiteAsync(site).ConfigureAwait(false);
            if (createResult is Result<SitesModel, AeroError>.Failure fail)
                return Results.Problem(fail.Error.ToString());

            var createdSite = ((Result<SitesModel, AeroError>.Ok)createResult).Value;

            // Create SiteHost entries for each host
            var allHosts = new List<(string host, bool isPrimary)>();
            if (request.PrimaryHost is not null)
                allHosts.Add((request.PrimaryHost, true));
            if (request.Hosts is not null)
            {
                var primaryNormalized = HostNormalizer.Normalize(request.PrimaryHost);
                foreach (var h in request.Hosts)
                {
                    var normalized = HostNormalizer.Normalize(h);
                    if (!string.IsNullOrWhiteSpace(normalized) && normalized != primaryNormalized)
                        allHosts.Add((normalized, false));
                }
            }
            if (allHosts.Count > 0)
                await siteService.ReplaceHostsAsync(createdSite.Id, allHosts);

            return Results.Created($"/api/v1/admin/sites/{createdSite.Id}", createdSite);
        });

        // PUT /api/v1/admin/sites/{id} — update site
        group.MapPut("/{id:long}", async (long id, UpdateSiteRequest request, ISiteService siteService) =>
        {
            if (id != request.Id)
                return Results.BadRequest("ID mismatch");

            var existing = await siteService.GetSiteByIdAsync(id);
            if (existing is not Option<SitesModel>.Some some)
                return Results.NotFound();

            var site = some.Value;
            site.Name = request.Name ?? site.Name;
            site.Description = request.Description ?? site.Description;

            var updateResult = await siteService.UpdateSiteAsync(site).ConfigureAwait(false);
            if (updateResult is Result<SitesModel, AeroError>.Failure uf)
                return Results.Problem(uf.Error.ToString());

            // Replace hosts
            var allHosts = new List<(string host, bool isPrimary)>();
            if (request.PrimaryHost is not null)
                allHosts.Add((request.PrimaryHost, true));
            if (request.Hosts is not null)
            {
                var primaryNormalized = HostNormalizer.Normalize(request.PrimaryHost);
                foreach (var h in request.Hosts)
                {
                    var normalized = HostNormalizer.Normalize(h);
                    if (!string.IsNullOrWhiteSpace(normalized) && normalized != primaryNormalized)
                        allHosts.Add((normalized, false));
                }
            }
            if (allHosts.Count > 0)
                await siteService.ReplaceHostsAsync(site.Id, allHosts);

            return Results.Ok(site);
        });

        // DELETE /api/v1/admin/sites/{id} — delete site (soft-delete, background job)
        group.MapDelete("/{id:long}", async (long id, ISiteService siteService) =>
        {
            var result = await siteService.DeleteSiteAsync(id);
            return result switch
            {
                Result<bool, AeroError>.Ok => Results.NoContent(),
                Result<bool, AeroError>.Failure f => Results.Problem(f.Error.ToString()),
                _ => Results.Problem("Unexpected result")
            };
        });

        return Task.CompletedTask;
    }
}


