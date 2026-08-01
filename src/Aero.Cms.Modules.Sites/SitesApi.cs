using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Modules.Sites.Events;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Claims;
using Wolverine;

namespace Aero.Cms.Modules.Sites;

/// <summary>
/// Maps manager endpoints for site selection, site administration, and client-error reporting.
/// </summary>
/// <remarks>
/// Every mapped endpoint requires an authenticated principal. Site-specific permission policies
/// for administration operations are applied in a later hardening phase.
/// </remarks>
public static class SitesApi
{
    /// <summary>
    /// Maps the sites and client-error endpoint groups.
    /// </summary>
    /// <param name="app">The route builder receiving the admin endpoints.</param>
    /// <remarks>Calling this method more than once registers duplicate route patterns.</remarks>
    public static void MapSitesApi(this IEndpointRouteBuilder app)
    {
        var sitesPath = $"/{HttpConstants.ApiPrefix}admin/sites";
        var group = app.MapGroup(sitesPath)
            .WithTags("Sites")
            .RequireAuthorization();

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
        group.MapPut("/{id:long}/style-profile", UpdateSiteStyleProfile);
        group.MapPut("/{id:long}/theme", UpdateSiteTheme);
        group.MapDelete("/{id:long}", DeleteSite);

        // ── Client error reporting ──
        var errorsGroup = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/errors")
            .WithTags("Admin - Error Reporting")
            .RequireAuthorization();
        errorsGroup.MapPost("/", ReportClientError);
    }

    // ──────────────────────────────────────────────
    //  Handler Methods
    // ──────────────────────────────────────────────

    /// <summary>
    /// Resolves the manager's selected-site cookie to a current site view.
    /// </summary>
    /// <param name="httpContext">The request containing the <c>AeroCms.SiteId</c> cookie.</param>
    /// <param name="siteLookup">The unscoped site lookup service.</param>
    /// <param name="userSiteService">The assignment service used to validate non-admin access.</param>
    /// <param name="cancellationToken">The request-aborted token.</param>
    /// <returns>An HTTP 200 response containing the matching site or a null body.</returns>
    /// <remarks>
    /// Non-admin callers must hold the site's <c>read</c> permission before its details are returned.
    /// </remarks>
    private static async Task<IResult> GetCurrentSite(
        HttpContext httpContext,
        [FromServices] ISiteLookupService siteLookup,
        [FromServices] IUserSiteService userSiteService,
        CancellationToken cancellationToken)
    {
        var cookie = httpContext.Request.Cookies["AeroCms.SiteId"];
        if (string.IsNullOrEmpty(cookie))
            return Results.Ok(null);

        if (!long.TryParse(cookie, out var siteId))
        {
            ExpireCurrentSiteCookie(httpContext);
            return Results.Ok(null);
        }

        var allSites = await siteLookup.GetAllAsync(cancellationToken);
        var site = allSites.FirstOrDefault(s => s.Id == siteId);
        if (site is null)
        {
            ExpireCurrentSiteCookie(httpContext);
            return Results.Ok(null);
        }

        if (!await CanReadSiteAsync(httpContext.User, siteId, userSiteService, cancellationToken))
            return Results.Forbid();

        return Results.Ok(site);
    }

    /// <summary>
    /// Validates a site identifier, writes the manager selection cookie, and publishes an audit event.
    /// </summary>
    /// <param name="request">The requested site selection.</param>
    /// <param name="httpContext">The current request and response context.</param>
    /// <param name="siteLookup">The unscoped lookup used to verify that the site exists.</param>
    /// <param name="userSiteService">The assignment service used to validate non-admin access.</param>
    /// <param name="bus">The message bus used for conditional audit publication.</param>
    /// <param name="cancellationToken">The request-aborted token.</param>
    /// <returns>Bad request, not found, or an empty HTTP 200 response.</returns>
    /// <remarks>
    /// Disabled sites are not rejected in this hardening phase. Non-admin callers must hold the
    /// site's <c>read</c> permission. The audit event is published only when the principal's
    /// name-identifier claim parses as a long, after the cookie has been appended to the response.
    /// </remarks>
    private static async Task<IResult> SetCurrentSite(
        [FromBody] SetCurrentSiteRequest request,
        HttpContext httpContext,
        [FromServices] ISiteLookupService siteLookup,
        [FromServices] IUserSiteService userSiteService,
        [FromServices] IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (request.SiteId <= 0)
            return Results.BadRequest("A valid site id is required.");

        if (!await CanReadSiteAsync(httpContext.User, request.SiteId, userSiteService, cancellationToken))
            return Results.Forbid();

        var sites = await siteLookup.GetAllAsync(cancellationToken);
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

    /// <summary>
    /// Determines whether the authenticated principal may read a selected site.
    /// </summary>
    /// <param name="user">The authenticated principal whose admin claims or identifier are evaluated.</param>
    /// <param name="siteId">The requested site identifier.</param>
    /// <param name="userSiteService">The service used for non-admin permission lookup.</param>
    /// <param name="cancellationToken">The request-aborted token.</param>
    /// <returns><see langword="true"/> for an administrator or an assigned user with read permission.</returns>
    /// <remarks>
    /// Administrators bypass assignment lookup. Other callers must expose a numeric Snowflake user
    /// identifier and hold the case-insensitive <c>read</c> permission for the requested site.
    /// </remarks>
    private static async Task<bool> CanReadSiteAsync(
        ClaimsPrincipal user,
        long siteId,
        IUserSiteService userSiteService,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole("Admin") || user.HasClaim("is_admin", "true"))
            return true;

        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return long.TryParse(claim, out var userId)
            && await userSiteService.HasPermissionAsync(
                userId,
                siteId,
                "read",
                cancellationToken);
    }

    /// <summary>Determines whether the principal may update the route-selected site.</summary>
    private static async Task<bool> CanUpdateSiteAsync(
        ClaimsPrincipal user,
        long siteId,
        IUserSiteService userSiteService,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole("Admin") || user.HasClaim("is_admin", "true"))
            return true;

        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return long.TryParse(claim, out var userId) &&
               await userSiteService.HasPermissionAsync(userId, siteId, "update", cancellationToken);
    }

    /// <summary>
    /// Expires the manager's selected-site cookie.
    /// </summary>
    /// <param name="httpContext">The response on which the deletion cookie is written.</param>
    /// <returns>An empty HTTP 200 response.</returns>
    private static IResult ClearCurrentSite(HttpContext httpContext)
    {
        ExpireCurrentSiteCookie(httpContext);
        return Results.Ok();
    }

    /// <summary>
    /// Writes an expired selected-site cookie using the same attributes as selection.
    /// </summary>
    /// <param name="httpContext">The response receiving the expired cookie.</param>
    private static void ExpireCurrentSiteCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete("AeroCms.SiteId", new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            IsEssential = true
        });
    }

    /// <summary>
    /// Returns every site view, including disabled sites.
    /// </summary>
    /// <param name="siteLookup">The unscoped site lookup service.</param>
    /// <returns>An HTTP 200 response containing all site views.</returns>
    private static async Task<IResult> ListSites(
        [FromServices] ISiteLookupService siteLookup)
    {
        var sites = await siteLookup.GetAllAsync();
        return Results.Ok(sites);
    }

    /// <summary>
    /// Returns the first repository page entry or creates a fallback default site.
    /// </summary>
    /// <param name="siteService">The service used for site and host persistence.</param>
    /// <param name="session">The injected document session; this handler does not use it.</param>
    /// <returns>The existing or created site, or an HTTP 500 problem response.</returns>
    /// <remarks>
    /// The method does not specifically filter for an enabled site despite its fallback intent.
    /// Default-site persistence commits before the localhost host is added, and the host-add result
    /// is ignored; a successful response can therefore contain a site without that host.
    /// </remarks>
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
            DefaultCulture = SitesModel.DefaultCultureName,
            SupportedCultures = [SitesModel.DefaultCultureName],
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

    /// <summary>
    /// Loads a site and enriches it with all assigned host records.
    /// </summary>
    /// <param name="id">The site identifier.</param>
    /// <param name="siteService">The service used for site lookup.</param>
    /// <param name="querySession">The session used to load host records.</param>
    /// <returns>An HTTP 200 site view or HTTP 404 when the site is absent.</returns>
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
            SupportedCultures = NormalizeCultureSettings(some.Value.DefaultCulture, some.Value.SupportedCultures).SupportedCultures,
            StyleProfile = SiteStyleProfileMapper.ToViewModel(some.Value.StyleProfile),
            ThemeId = some.Value.ThemeId,
            ThemeVersion = some.Value.ThemeVersion,
            ThemeRevision = some.Value.ThemeRevision,
            CreatedOn = some.Value.CreatedOn,
            ModifiedOn = some.Value.ModifiedOn,
            CreatedBy = some.Value.CreatedBy,
            ModifiedBy = some.Value.ModifiedBy
        };
        return Results.Ok(vm);
    }

    /// <summary>
    /// Creates a site, replaces its host assignments, and seeds published home and not-found pages.
    /// </summary>
    /// <param name="request">The site metadata, cultures, and host values to create.</param>
    /// <param name="siteService">The service used to persist the site and hosts.</param>
    /// <param name="session">The document session used to seed pages.</param>
    /// <param name="httpContext">The request supplying tenant-selection and audit-user claims.</param>
    /// <returns>A validation problem, persistence problem, or HTTP 201 response containing the site.</returns>
    /// <remarks>
    /// If the selected-site cookie resolves, its tenant identifier is inherited; otherwise a new
    /// tenant identifier is generated without creating a tenant document. Site, host, homepage, and
    /// not-found-page work is not one transaction: the site is committed first, the host replacement
    /// result is ignored, and each seed page is saved separately. A later failure can therefore leave
    /// a partially initialized site.
    /// </remarks>
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

        var cultureSettings = NormalizeCultureSettings(request.DefaultCulture, request.SupportedCultures);
        var site = new SitesModel
        {
            Id = Snowflake.NewId(),
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            IsEnabled = true,
            DefaultCulture = cultureSettings.DefaultCulture,
            SupportedCultures = cultureSettings.SupportedCultures
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

    /// <summary>
    /// Updates mutable site metadata and optionally replaces host assignments.
    /// </summary>
    /// <param name="id">The route identifier, which must equal the request identifier.</param>
    /// <param name="request">The replacement metadata and optional hosts.</param>
    /// <param name="siteService">The service used for lookup and persistence.</param>
    /// <returns>Bad request, not found, persistence problem, or an HTTP 200 site document.</returns>
    /// <remarks>
    /// The site update commits before host replacement. Replacement occurs only when at least one
    /// primary or secondary host candidate is collected, so an empty request cannot clear existing
    /// hosts. Its result is ignored when attempted, so the returned site does not guarantee that
    /// host changes succeeded.
    /// </remarks>
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
        var cultureSettings = NormalizeCultureSettings(request.DefaultCulture ?? site.DefaultCulture, request.SupportedCultures ?? site.SupportedCultures);
        site.DefaultCulture = cultureSettings.DefaultCulture;
        site.SupportedCultures = cultureSettings.SupportedCultures;

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

    /// <summary>
    /// Applies an optimistic-revision style-profile update and maps domain failures to HTTP results.
    /// </summary>
    /// <param name="id">The site identifier.</param>
    /// <param name="request">The expected revision and proposed settings.</param>
    /// <param name="styleProfileService">The style-profile mutation service.</param>
    /// <param name="cancellationToken">The request-aborted token.</param>
    /// <returns>
    /// HTTP 200, 404, 409, or 400 for recognized results; otherwise an HTTP 500 problem response.
    /// </returns>
    private static async Task<IResult> UpdateSiteStyleProfile(
        long id,
        [FromBody] UpdateSiteStyleProfileRequest request,
        [FromServices] ISiteStyleProfileService styleProfileService,
        CancellationToken cancellationToken)
    {
        var result = await styleProfileService.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            Result<SiteStyleProfileViewModel, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<SiteStyleProfileViewModel, AeroError>.Failure { Error: AeroError.NotFound notFound } =>
                Results.NotFound(new { message = notFound.msg }),
            Result<SiteStyleProfileViewModel, AeroError>.Failure { Error: AeroError.Conflict conflict } =>
                Results.Conflict(new { message = conflict.msg }),
            Result<SiteStyleProfileViewModel, AeroError>.Failure { Error: AeroError.Validation validation } =>
                Results.BadRequest(new { errors = validation.Errors }),
            Result<SiteStyleProfileViewModel, AeroError>.Failure { Error: var error } =>
                Results.Problem(error.ToString(), statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Problem("Unexpected style-profile update result.")
        };
    }

    /// <summary>Applies an authorized optimistic exact-theme update to the route-selected site.</summary>
    private static async Task<IResult> UpdateSiteTheme(
        long id,
        [FromBody] UpdateSiteThemeRequest request,
        HttpContext httpContext,
        [FromServices] IUserSiteService userSiteService,
        [FromServices] ISiteThemeSelectionService themeSelectionService,
        CancellationToken cancellationToken)
    {
        if (!await CanUpdateSiteAsync(httpContext.User, id, userSiteService, cancellationToken))
            return Results.Forbid();

        var result = await themeSelectionService.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            Result<SiteThemeSelectionViewModel, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<SiteThemeSelectionViewModel, AeroError>.Failure { Error: AeroError.NotFound notFound } =>
                Results.NotFound(new { message = notFound.msg }),
            Result<SiteThemeSelectionViewModel, AeroError>.Failure { Error: AeroError.Conflict conflict } =>
                Results.Conflict(new { message = conflict.msg }),
            Result<SiteThemeSelectionViewModel, AeroError>.Failure { Error: AeroError.Validation validation } =>
                Results.BadRequest(new { errors = validation.Errors }),
            Result<SiteThemeSelectionViewModel, AeroError>.Failure =>
                Results.Problem("The site theme could not be updated.", statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Problem("Unexpected site-theme update result.")
        };
    }

    /// <summary>
    /// Deletes a site and maps the railway result to an HTTP response.
    /// </summary>
    /// <param name="id">The site identifier to delete.</param>
    /// <param name="httpContext">The request whose selection cookie may reference the deleted site.</param>
    /// <param name="siteService">The site mutation service.</param>
    /// <returns>HTTP 204 on success or an HTTP 500 problem response on failure.</returns>
    /// <remarks>Related assignments and content are not removed by this endpoint.</remarks>
    private static async Task<IResult> DeleteSite(
        long id,
        HttpContext httpContext,
        [FromServices] ISiteService siteService)
    {
        var result = await siteService.DeleteSiteAsync(id);
        if (result is Result<bool, AeroError>.Ok)
        {
            var selectedSite = httpContext.Request.Cookies["AeroCms.SiteId"];
            if (long.TryParse(selectedSite, out var selectedSiteId) && selectedSiteId == id)
            {
                ExpireCurrentSiteCookie(httpContext);
            }

            return Results.NoContent();
        }

        return result switch
        {
            Result<bool, AeroError>.Failure f => Results.Problem(f.Error.ToString()),
            _ => Results.Problem("Unexpected result")
        };
    }

    /// <summary>
    /// Logs and republishes a manager-supplied client error.
    /// </summary>
    /// <param name="entry">The client-supplied diagnostic payload.</param>
    /// <param name="bus">The message bus receiving the error event.</param>
    /// <param name="loggerFactory">The factory used to create the reporting logger.</param>
    /// <returns>An empty HTTP 200 response, or HTTP 500 when logging or publication throws.</returns>
    /// <remarks>The payload is accepted as supplied and may contain user agent, URL, and stack-trace data.</remarks>
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

    /// <summary>
    /// Creates and commits a published homepage shell for a newly created site.
    /// </summary>
    /// <param name="session">The document session used for the page commit.</param>
    /// <param name="site">The owning site and source of title and culture defaults.</param>
    /// <param name="createdBy">The audit identity stored on the page.</param>
    /// <returns>A task that completes after the independent page commit.</returns>
    /// <remarks>The page receives no draft or published HTML content in this helper.</remarks>
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
            Culture = site.DefaultCulture ?? SitesModel.DefaultCultureName,
            PublicationState = ContentPublicationState.Published,
            PublishedOn = now,
            CreatedOn = now,
            ModifiedOn = now,
            CreatedBy = createdBy,
            ModifiedBy = createdBy
        };

        page.TranslationGroupId = page.Id;
        session.Store(page);
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Creates and commits a published not-found page with matching draft and published HTML trees.
    /// </summary>
    /// <param name="session">The document session used for the page commit.</param>
    /// <param name="site">The owning site and source of culture defaults.</param>
    /// <param name="createdBy">The audit identity stored on the page.</param>
    /// <returns>A task that completes after the independent page commit.</returns>
    private static async Task CreateOopsPageAsync(IDocumentSession session, SitesModel site, string createdBy)
    {
        var now = DateTimeOffset.UtcNow;

        var content = new HtmlPageContent();
        var section = HtmlNode.CreateElement("section");
        var heading = HtmlNode.CreateElement("h1");
        heading.Children.Add(HtmlNode.CreateText("Page Not Found"));
        var summary = HtmlNode.CreateElement("p");
        summary.Children.Add(HtmlNode.CreateText("The page you're looking for doesn't exist or has been moved."));
        section.Children.Add(heading);
        section.Children.Add(summary);
        content.Root.Children.Add(section);

        var page = new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "oops",
            Title = "Oops",
            Summary = "Page not found",
            SiteId = site.Id,
            Culture = site.DefaultCulture ?? SitesModel.DefaultCultureName,
            PublicationState = ContentPublicationState.Published,
            PublishedOn = now,
            CreatedOn = now,
            ModifiedOn = now,
            CreatedBy = createdBy,
            ModifiedBy = createdBy,
            DraftContent = content,
            PublishedContent = HtmlTreeOperations.ClonePreservingNodeIds(content),
            ContentRevision = 1,
            PublishedVersion = 1
        };

        page.TranslationGroupId = page.Id;
        session.Store(page);
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Canonicalizes culture names, removes invalid and duplicate entries, and includes the default.
    /// </summary>
    /// <param name="defaultCulture">The preferred default culture, or a value that may be invalid.</param>
    /// <param name="supportedCultures">Candidate supported cultures.</param>
    /// <returns>
    /// A canonical default and case-insensitively distinct supported list containing that default.
    /// Invalid defaults fall back to the model's default culture.
    /// </returns>
    private static (string DefaultCulture, List<string> SupportedCultures) NormalizeCultureSettings(
        string? defaultCulture,
        IEnumerable<string>? supportedCultures)
    {
        var cultures = (supportedCultures ?? [])
            .Select(NormalizeCultureNameOrNull)
            .Where(static x => x is not null)
            .Select(static x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedDefault = NormalizeCultureNameOrNull(defaultCulture) ?? SitesModel.DefaultCultureName;
        if (!cultures.Contains(normalizedDefault, StringComparer.OrdinalIgnoreCase))
        {
            cultures.Insert(0, normalizedDefault);
        }

        return (normalizedDefault, cultures.Count == 0 ? [SitesModel.DefaultCultureName] : cultures);
    }

    /// <summary>
    /// Resolves a trimmed culture name to the platform's canonical spelling.
    /// </summary>
    /// <param name="culture">The candidate culture name.</param>
    /// <returns>The canonical culture name, or <see langword="null"/> for blank or unknown values.</returns>
    private static string? NormalizeCultureNameOrNull(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return null;

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Selects the first available display identity for seeded-content audit fields.
    /// </summary>
    /// <param name="user">The current principal.</param>
    /// <returns>
    /// Identity name, email claim, name-identifier claim, or <c>system</c>, in that order.
    /// </returns>
    private static string ResolveAuditUser(ClaimsPrincipal user)
    {
        return user.Identity?.Name
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }
}
