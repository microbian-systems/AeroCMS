using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Sites.Events;
using Aero.Core;
using Aero.Core.Railway;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Wolverine;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Admin API for site management.
/// </summary>
public static class SitesApi
{
    /// <summary>
    /// Maps the Sites Admin API endpoints.
    /// </summary>
    public static void MapSitesApi(this IEndpointRouteBuilder app)
    {
        var sitesPath = $"/{HttpConstants.ApiPrefix}admin/sites";
        var group = app.MapGroup(sitesPath).WithTags("Sites");

        // ── Current site (cookie-based selection) ──
        group.MapGet("/current", GetCurrentSite);
        group.MapPost("/current", SetCurrentSite);
        group.MapDelete("/current", ClearCurrentSite);

        // ── Site CRUD ──
        group.MapGet("/", ListSites);
        group.MapGet("/default", GetDefaultSite);
        group.MapGet("/{id:long}", GetSiteById);
        group.MapPost("/", CreateSite);
        group.MapPut("/{id:long}", UpdateSite);
        group.MapDelete("/{id:long}", DeleteSite);

        // ── Client error reporting ──
        var errorsGroup = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/errors")
            .WithTags("Admin - Error Reporting");
        errorsGroup.MapPost("/", ReportClientError);
    }

    // ──────────────────────────────────────────────
    //  Handler Methods
    // ──────────────────────────────────────────────

    private static async Task<IResult> GetCurrentSite(
        HttpContext httpContext,
        [FromServices] ISiteLookupService siteLookup)
    {
        var cookie = httpContext.Request.Cookies["AeroCms.SiteId"];
        if (string.IsNullOrEmpty(cookie) || !long.TryParse(cookie, out var siteId))
            return Results.Ok(null);

        var allSites = await siteLookup.GetAllAsync();
        var site = allSites.FirstOrDefault(s => s.Id == siteId);
        return Results.Ok(site);
    }

    private static async Task<IResult> SetCurrentSite(
        [FromBody] SetCurrentSiteRequest request,
        HttpContext httpContext,
        [FromServices] ISiteLookupService siteLookup,
        [FromServices] IMessageBus bus)
    {
        if (request.SiteId <= 0)
            return Results.BadRequest("A valid site id is required.");

        var sites = await siteLookup.GetAllAsync();
        if (!sites.Any(site => site.Id == request.SiteId))
            return Results.NotFound();

        var siteId = request.SiteId;
        httpContext.Response.Cookies.Append("AeroCms.SiteId", siteId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            IsEssential = true,
            Secure = httpContext.Request.IsHttps
        });

        // Publish audit event via Wolverine
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userId, out var uid))
        {
            await bus.PublishAsync(new SiteSelectionChanged(siteId, uid, DateTimeOffset.UtcNow));
        }

        return Results.Ok();
    }

    private static IResult ClearCurrentSite(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("AeroCms.SiteId", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps
        });
        return Results.Ok();
    }

    private static async Task<IResult> ListSites(
        [FromServices] ISiteLookupService siteLookup)
    {
        var sites = await siteLookup.GetAllAsync();
        return Results.Ok(sites);
    }

    private static async Task<IResult> GetDefaultSite(
        [FromServices] ISiteService siteService,
        [FromServices] IDocumentSession session)
    {
        // Try to find any enabled site first
        var sites = await siteService.GetAllSitesAsync(page: 1, num: 1);
        if (sites is Result<IEnumerable<SitesModel>, AeroError>.Ok ok && ok.Value.Any())
            return Results.Ok(ok.Value.First());

        // No sites exist — create a default one with a new tenant
        var site = new SitesModel
        {
            Id = Snowflake.NewId(),
            TenantId = Snowflake.NewId(),
            Name = "Default Site",
            IsEnabled = true,
            DefaultCulture = "en-US",
            Description = "Auto-created default site"
        };

        var createResult = await siteService.CreateSiteAsync(site);
        if (createResult is Result<SitesModel, AeroError>.Ok created)
        {
            await siteService.AddHostAsync(created.Value.Id, "localhost", isPrimary: true);
            return Results.Ok(created.Value);
        }

        return Results.Problem("Failed to create default site");
    }

    private static async Task<IResult> GetSiteById(
        long id,
        [FromServices] ISiteService siteService,
        [FromServices] IQuerySession querySession)
    {
        var site = await siteService.GetSiteByIdAsync(id);
        if (site is not Option<SitesModel>.Some some)
            return Results.NotFound();

        // Enrich with host records so the client gets PrimaryHost and Hosts
        var hosts = await querySession.Query<SiteHost>()
            .Where(h => h.SiteId == id)
            .ToListAsync();

        var hostList = hosts.Select(h => h.Host).ToList();
        var vm = new SiteViewModel
        {
            Id = some.Value.Id,
            TenantId = some.Value.TenantId,
            Name = some.Value.Name,
            PrimaryHost = hosts.FirstOrDefault(h => h.IsPrimary)?.Host ?? hosts.FirstOrDefault()?.Host,
            Hosts = hostList,
            IsEnabled = some.Value.IsEnabled,
            DefaultCulture = some.Value.DefaultCulture,
            CreatedOn = some.Value.CreatedOn,
            ModifiedOn = some.Value.ModifiedOn,
            CreatedBy = some.Value.CreatedBy,
            ModifiedBy = some.Value.ModifiedBy
        };
        return Results.Ok(vm);
    }

    private static async Task<IResult> CreateSite(
        [FromBody] CreateSiteRequest request,
        [FromServices] ISiteService siteService,
        [FromServices] IDocumentSession session,
        HttpContext httpContext)
    {
        var validator = new Abstractions.Validators.SiteRequestValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // Resolve TenantId from the currently selected site's cookie
        var tenantId = Snowflake.NewId(); // fallback if no current site exists
        var siteIdCookie = httpContext.Request.Cookies["AeroCms.SiteId"];
        if (long.TryParse(siteIdCookie, out var currentSiteId))
        {
            var currentSite = await siteService.GetSiteByIdAsync(currentSiteId);
            if (currentSite is Option<SitesModel>.Some someSite)
                tenantId = someSite.Value.TenantId;
        }

        var site = new SitesModel
        {
            Id = Snowflake.NewId(),
            TenantId = tenantId,
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

        // Auto-create default pages so the new site has initial content
        var createdBy = ResolveAuditUser(httpContext.User);
        await CreateDefaultHomepageAsync(session, createdSite, createdBy);
        await CreateOopsPageAsync(session, createdSite, createdBy);

        return Results.Created($"/api/v1/admin/sites/{createdSite.Id}", createdSite);
    }

    private static async Task<IResult> UpdateSite(
        long id,
        [FromBody] UpdateSiteRequest request,
        [FromServices] ISiteService siteService)
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
    }

    private static async Task<IResult> DeleteSite(
        long id,
        [FromServices] ISiteService siteService)
    {
        var result = await siteService.DeleteSiteAsync(id);
        return result switch
        {
            Result<bool, AeroError>.Ok => Results.NoContent(),
            Result<bool, AeroError>.Failure f => Results.Problem(f.Error.ToString()),
            _ => Results.Problem("Unexpected result")
        };
    }

    private static async Task<IResult> ReportClientError(
        [FromBody] ClientErrorEntry entry,
        [FromServices] IMessageBus bus,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ClientErrorReporting");
        try
        {
            logger.LogWarning("Client error reported: {ErrorType} — {ErrorMessage} at {ClientUrl}",
                entry.ErrorType, entry.ErrorMessage, entry.ClientUrl);

            await bus.PublishAsync(new ClientErrorReported(
                entry.ErrorType,
                entry.ErrorMessage,
                entry.ClientUrl,
                entry.UserAgent,
                entry.ClientTimestamp,
                entry.StackTrace
            ));

            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process client error report");
            return Results.Problem("Failed to process error report");
        }
    }

    // ──────────────────────────────────────────────
    //  Seed Helpers
    // ──────────────────────────────────────────────

    private static async Task CreateDefaultHomepageAsync(IDocumentSession session, SitesModel site, string createdBy)
    {
        var now = DateTimeOffset.UtcNow;
        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Homepage,
            Slug = "/",
            Title = site.Name ?? "Home",
            Summary = $"Welcome to {site.Name}",
            SiteId = site.Id,
            PublicationState = ContentPublicationState.Published,
            PublishedOn = now,
            CreatedOn = now,
            ModifiedOn = now,
            CreatedBy = createdBy,
            ModifiedBy = createdBy
        };

        session.Store(page);
        await session.SaveChangesAsync();
    }

    private static async Task CreateOopsPageAsync(IDocumentSession session, SitesModel site, string createdBy)
    {
        var now = DateTimeOffset.UtcNow;

        var heroBlock = new BoringHeroBlock
        {
            Id = Snowflake.NewId(),
            Title = "Page Not Found",
            Summary = "The page you're looking for doesn't exist or has been moved.",
            FullWidth = true,
            Order = 0
        };

        session.Store(heroBlock);

        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "oops",
            Title = "Oops",
            Summary = "Page not found",
            SiteId = site.Id,
            PublicationState = ContentPublicationState.Published,
            PublishedOn = now,
            CreatedOn = now,
            ModifiedOn = now,
            CreatedBy = createdBy,
            ModifiedBy = createdBy,
            LayoutRegions =
            [
                new LayoutRegion
                {
                    Name = "Main",
                    Order = 0,
                    Columns =
                    [
                        new LayoutColumn
                        {
                            Width = 12,
                            Blocks =
                            [
                                new BlockPlacement
                                {
                                    BlockId = heroBlock.Id,
                                    BlockType = heroBlock.BlockType,
                                    Order = 0
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        session.Store(page);
        await session.SaveChangesAsync();
    }

    private static string ResolveAuditUser(ClaimsPrincipal user)
    {
        return user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }
}
