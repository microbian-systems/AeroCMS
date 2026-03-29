using System.Security.Claims;
using Aero.Cms.Core.Audit;
using Aero.Cms.Core.Http.Clients;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;


/// <summary>
/// Summary information for a page (used in list responses).
/// </summary>
/// <param name="Id">The page ID.</param>
/// <param name="Slug">The page slug.</param>
/// <param name="Title">The page title.</param>
/// <param name="Summary">The page summary.</param>
/// <param name="PublishedOn">When the page was published.</param>
public record PageSummaryV2(long Id, string Slug, string Title, string? Summary, DateTimeOffset? PublishedOn);


[Obsolete("only here for ref purposes", true)]
public static class PagesApiV2
{
    /// <summary>
    /// Maps the Pages API endpoints.
    /// </summary>
    public static void MapPagesApiV2(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/pages/{culture}/{slug}", GetPageByCultureAndSlug)
            .WithName("GetPageByCultureAndSlug")
            .WithTags("Pages");

        app.MapGet("/api/v1/pages", ListPages)
            .WithName("ListPages")
            .WithTags("Pages");

        app.MapPost("/api/v1/pages", CreatePage)
            .WithName("CreatePage")
            .WithTags("Pages");

        app.MapPut("/api/v1/pages/{id}", UpdatePage)
            .WithName("UpdatePage")
            .WithTags("Pages");
    }

    private static async Task<IResult> GetPageByCultureAndSlug(
        string culture,
        string slug,
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApiV2));
        try
        {
            var result = await pageService.FindBySlugAsync(slug, cancellationToken);

            if (result is Result<string, PageDocument>.Failure failure)
            {
                logger.LogWarning("Page not found for culture={Culture}, slug={Slug}: {Error}",
                    culture, slug, failure.Error);
                return Results.NotFound(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument>.Ok ok)
            {
                return Results.Ok(ok.Value);
            }

            return Results.Json(new { error = "An unexpected error occurred." }, statusCode: 500);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for culture={Culture}, slug={Slug}", culture, slug);
            return Results.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> ListPages(
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApiV2));
        // Query published pages directly using the service's session-like pattern
        // For now, return empty array - the service doesn't have a list method
        // This would need to be extended to query all pages and filter by PublicationState
        return Results.Ok(Array.Empty<PageSummaryV2>());
    }

    private static async Task<IResult> CreatePage(
        [FromBody] CreatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] IAuditService auditService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check slug uniqueness
            var normalizedSlug = ContentSlugDocument.Normalize(request.Slug);
            var existingSlug = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(s => s.NormalizedSlug == normalizedSlug, cancellationToken);
            if (existingSlug != null)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Slug already exists",
                    Detail = $"The slug '{request.Slug}' is already reserved by another page"
                });
            }

            var page = new PageDocument
            {
                Id = Snowflake.NewId(),
                Title = request.Title,
                Slug = request.Slug,
                Summary = request.Summary,
                SeoTitle = request.SeoTitle,
                SeoDescription = request.SeoDescription,
                PublicationState = request.PublicationState
            };

            var result = await pageService.SaveAsync(page, cancellationToken);

            if (result is Result<string, PageDocument>.Failure failure)
            {
                return TypedResults.BadRequest(new { error = failure.Error });
            }

            if (result is Result<string, PageDocument>.Ok ok)
            {
                var userId = GetUserId(httpContextAccessor);
                var auditEvent = PageCreatedEvent.Create(userId, page.Id, page.Title, page.Slug, (Aero.Cms.Core.Audit.PageKind)page.Kind);
                await auditService.LogAsync(auditEvent, cancellationToken);
                return TypedResults.Ok(ok.Value);
            }

            return TypedResults.BadRequest(new { error = "An unexpected error occurred." });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] UpdatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] IAuditService auditService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IDocumentSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            // Check slug uniqueness (excluding current page)
            var normalizedSlug = ContentSlugDocument.Normalize(request.Slug);
            var existingSlug = await session.Query<ContentSlugDocument>()
                .FirstOrDefaultAsync(s => s.NormalizedSlug == normalizedSlug && s.OwnerId != id, cancellationToken);
            if (existingSlug != null)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Slug already exists",
                    Detail = $"The slug '{request.Slug}' is already reserved by another page"
                });
            }

            var loadResult = await pageService.LoadAsync(id, cancellationToken);

            if (loadResult is Result<string, PageDocument?>.Failure failure)
            {
                return TypedResults.NotFound(new { error = failure.Error });
            }

            if (loadResult is Result<string, PageDocument?>.Ok { Value: null })
            {
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found" });
            }

            if (loadResult is Result<string, PageDocument?>.Ok { Value: not null } ok)
            {
                var page = ok.Value;
                page.Title = request.Title;
                page.Slug = request.Slug;
                page.Summary = request.Summary;
                page.SeoTitle = request.SeoTitle;
                page.SeoDescription = request.SeoDescription;
                page.PublicationState = request.PublicationState;

                var saveResult = await pageService.SaveAsync(page, cancellationToken);

                if (saveResult is Result<string, PageDocument>.Failure saveFailure)
                {
                    return TypedResults.BadRequest(new { error = saveFailure.Error });
                }

                if (saveResult is Result<string, PageDocument>.Ok saveOk)
                {
                    var userId = GetUserId(httpContextAccessor);
                    var auditEvent = PageUpdatedEvent.Create(userId, page.Id, page.Title, page.Slug);
                    await auditService.LogAsync(auditEvent, cancellationToken);
                    return TypedResults.Ok(saveOk.Value);
                }
            }

            return TypedResults.BadRequest(new { error = "An unexpected error occurred." });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static long GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return 0;
    }
}