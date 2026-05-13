using Wolverine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Http.Clients;
using CreatePageRequest = Aero.Cms.Abstractions.Requests.CreatePageRequest;
using UpdatePageRequest = Aero.Cms.Abstractions.Requests.UpdatePageRequest;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for page content management.
/// </summary>
public static class PagesApi
{
    /// <summary>
    /// Maps the Pages Admin API endpoints.
    /// </summary>
    public static void MapPagesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/pages")
            .WithTags("Admin - Pages");

        group.MapGet("/", ListPages)
            .WithName("ListPages");

        group.MapGet("/{id:long}", GetPageById)
            .WithName("GetPageById");

        group.MapGet("/slug/{*slug}", GetPageBySlug)
            .WithName("GetPageBySlug");

        group.MapGet("/drafts/{id:long}", PreviewDraftPage)
            .WithName("PreviewDraftPage");

        group.MapPost("/", CreatePage)
            .WithName("CreatePage");

        group.MapPut("/{id:long}", UpdatePage)
            .WithName("UpdatePage");

        group.MapDelete("/{id:long}", DeletePage)
            .WithName("DeletePage");

        group.MapDelete("/{id:long}/cascade", DeletePageCascade)
            .WithName("DeletePageCascade");

        group.MapPost("/delete-multiple", DeleteMultiplePages)
            .WithName("DeleteMultiplePages");

        group.MapPut("/{id:long}/publish", PublishPage)
            .WithName("PublishPage");

        group.MapPut("/{id:long}/unpublish", UnpublishPage)
            .WithName("UnpublishPage");

        // Draft endpoints — auto-save writes here, manual save/publish promote to PageDocument
        group.MapGet("/{id:long}/draft", GetPageDraft)
            .WithName("GetPageDraft");

        group.MapPut("/{id:long}/draft", SavePageDraft)
            .WithName("SavePageDraft");

        group.MapDelete("/{id:long}/draft", DeletePageDraft)
            .WithName("DeletePageDraft");

        // Event sourcing — version history
        group.MapGet("/{id:long}/events", GetPageEvents)
            .WithName("GetPageEvents");
    }

    private static async Task<IResult> ListPages(
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.GetAllPagesAsync(skip, take, search, cancellationToken);
            if (result is Result<(IReadOnlyList<PageDocument> Items, long TotalCount), AeroError>.Ok ok)
            {
                var summary = ok.Value.Items.Select(p => new PageSummary(
                    p.Id, 
                    p.Title, 
                    p.Slug, 
                    p.CreatedOn.DateTime, 
                    p.PublishedOn?.DateTime, 
                    p.Summary)).ToList();

                return TypedResults.Ok(new PagedResult<PageSummary>(summary, ok.Value.TotalCount, skip, take));
            }

            return TypedResults.Problem("Failed to list pages");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pages");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetPageById(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.LoadAsync(id, cancellationToken);
            if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for id={Id}", id);
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.FindBySlugAsync(slug, cancellationToken);
            if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving page for slug={Slug}", slug);
            return TypedResults.NotFound();
        }
    }

    private static IResult PreviewDraftPage(long id, [FromQuery] long? previewVersion = null)
    {
        var url = previewVersion is { } version
            ? $"/_cms/preview/pages/drafts/{id}?previewVersion={version}"
            : $"/_cms/preview/pages/drafts/{id}";

        return TypedResults.Redirect(url);
    }

    private static async Task<IResult> CreatePage(
        [FromBody] Aero.Cms.Abstractions.Http.Clients.CreatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var moduleRequest = new CreatePageRequest(
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                request.LayoutRegions,
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                request.EditorBlocks
            );

            var result = await pageService.CreateAsync(moduleRequest, cancellationToken);
            if (result is Result<PageDocument, AeroError>.Ok ok)
            {
                return TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/pages/{ok.Value.Id}", MapToDetail(ok.Value));
            }

            if (result is Result<PageDocument, AeroError>.Failure failure)
            {
                logger.LogWarning("Failed to create page. Error: {Error}. Request: {@Request}", failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page. Request: {@Request}", request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] Aero.Cms.Abstractions.Http.Clients.UpdatePageRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var moduleRequest = new UpdatePageRequest(
                id,
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                request.LayoutRegions,
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                request.EditorBlocks
            );

            var result = await pageService.UpdateAsync(id, moduleRequest, cancellationToken);
            if (result is Result<PageDocument, AeroError>.Ok ok)
            {
                return TypedResults.Ok(MapToDetail(ok.Value));
            }

            if (result is Result<PageDocument, AeroError>.Failure failure)
            {
                logger.LogError("Failed to update page {Id}. Error: {Error}. Request: {@Request}", id, failure.Error, request);
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page",
                    Detail = failure.Error.ToString(),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return TypedResults.Problem("An unexpected error occurred");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating page for id={Id}. Request: {@Request}", id, request);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeletePage(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] bool deleteDescendants = false,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.DeleteAsync(id, deleteDescendants, cancellationToken);
            if (result is Result<bool, AeroError>.Ok { Value: true })
            {
                return TypedResults.Ok(true);
            }

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting page for id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete page",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DeletePageCascade(
        long id,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.DeleteAsync(id, deleteDescendants: true, cancellationToken);
            if (result is Result<bool, AeroError>.Ok { Value: true })
                return TypedResults.Ok(true);

            return TypedResults.NotFound();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cascade-deleting page id={Id}", id);
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to cascade-delete page",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> DeleteMultiplePages(
        [FromBody] DeleteMultiplePagesRequest request,
        [FromServices] IPageContentService pageService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.DeleteMultipleAsync(request.Ids, request.DeleteDescendants, cancellationToken);
            if (result is Result<int, AeroError>.Ok ok)
                return TypedResults.Ok(new { deleted = ok.Value });

            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to delete pages",
                Detail = result is Result<int, AeroError>.Failure f ? f.Error.ToString() : "Unknown error"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error bulk-deleting pages");
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Failed to bulk-delete pages",
                Detail = ex.Message
            });
        }
    }

    private static async Task<IResult> PublishPage(
        long id,
        IDocumentSession session,
        IMessageBus bus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null)
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });

            session.Events.Append($"page-{id}", new PageStateChanged(ContentPublicationState.Published));
            await session.SaveChangesAsync(cancellationToken);

            // Publish cache-invalidation event so the OutputCache "PagesPolicy"
            // (tagged "pages-list") and FusionCache entries are evicted.
            // Without this, CDN/browser caches may serve stale page content
            // after a publish action. The ContentUpdatedHandler picks this up
            // and invalidates both cache layers in a single handler call.
            await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));

            logger.LogInformation("Published page id={Id}, slug={Slug}", id, page.Slug);
            return TypedResults.Ok(MapToDetail(page));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing page id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UnpublishPage(
        long id,
        IDocumentSession session,
        IMessageBus bus,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var page = await session.LoadAsync<PageDocument>(id, cancellationToken);

            if (page is null)
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });

            session.Events.Append($"page-{id}", new PageStateChanged(ContentPublicationState.Draft));
            await session.SaveChangesAsync(cancellationToken);

            // Same cache eviction as PublishPage — unpublishing changes
            // the visible state of the page, so cached copies must be evicted.
            await bus.PublishAsync(new PageContentUpdatedEvent(page.Id, page.SiteId, page.Slug, page.Slug));

            logger.LogInformation("Unpublished page id={Id}, slug={Slug}", id, page.Slug);
            return TypedResults.Ok(MapToDetail(page));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing page id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static PageDetail MapToDetail(PageDocument p)
    {
        return new PageDetail(
            p.Id,
            p.Title,
            p.Slug,
            p.Summary,
            p.SeoTitle,
            p.SeoDescription,
            p.CreatedOn.DateTime,
            (p.ModifiedOn ?? p.CreatedOn).DateTime,
            p.PublishedOn?.DateTime,
            p.PublicationState,
            p.Blocks.Count,
            p.ShowInNavMenu,
            p.ShowHeaderNavigation,
            p.HideFooter,
            p.ShowChatAgent,
            p.Blocks,
            p.ParentId,
            p.Path,
            p.Depth
        );
    }

    // ── Draft handlers ─────────────────────────────────────────

    private static async Task<IResult> GetPageDraft(
        long id,
        IQuerySession querySession)
    {
        var draft = await querySession.Query<PageDraft>()
            .FirstOrDefaultAsync(d => d.PageId == id);

        return TypedResults.Ok(draft);  // returns null (not 404) when no draft exists
    }

    private static async Task<IResult> SavePageDraft(
        long id,
        PageDraftRequest request,
        IDocumentSession session,
        IQuerySession querySession)
    {
        // Resolve SiteId from the existing page
        var page = await querySession.LoadAsync<PageDocument>(id);
        if (page is null)
            return TypedResults.NotFound();

        // Find existing draft or create new
        var existing = await querySession.Query<PageDraft>()
            .FirstOrDefaultAsync(d => d.PageId == id);

        if (existing is not null)
        {
            existing.Title = request.Title;
            existing.Slug = request.Slug;
            existing.Summary = request.Summary;
            existing.Blocks = request.Blocks ?? [];
            existing.DraftedAt = DateTimeOffset.UtcNow;
            session.Store(existing);
        }
        else
        {
            var draft = new PageDraft
            {
                Id = Snowflake.NewId(),
                SiteId = page.SiteId,
                PageId = id,
                Title = request.Title,
                Slug = request.Slug,
                Summary = request.Summary,
                Blocks = request.Blocks ?? [],
                DraftedAt = DateTimeOffset.UtcNow
            };
            session.Store(draft);
        }

        await session.SaveChangesAsync();
        return TypedResults.Ok();
    }

    private static async Task<IResult> DeletePageDraft(
        long id,
        IDocumentSession session,
        IQuerySession querySession)
    {
        var existing = await querySession.Query<PageDraft>()
            .FirstOrDefaultAsync(d => d.PageId == id);

        if (existing is not null)
        {
            session.Delete(existing);
            await session.SaveChangesAsync();
        }

        return TypedResults.NoContent();
    }

    // ── Event sourcing — version history ────────────────────────

    private static async Task<IResult> GetPageEvents(
        long id,
        IQuerySession querySession,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var streamKey = $"page-{id}";

            // Verify the page exists first
            var page = await querySession.LoadAsync<PageDocument>(id, cancellationToken);
            if (page is null)
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });

            // Fetch all events from the stream (version history)
            var events = await querySession.Events.FetchStreamAsync(streamKey, token: cancellationToken);

            var history = events.Select(e => new PageEventItem(
                Version: e.Version,
                EventType: e.EventType.Name,
                Timestamp: e.Timestamp,
                StreamKey: e.StreamKey ?? streamKey,
                IsArchived: e.IsArchived
            )).ToList();

            return TypedResults.Ok(new PageEventHistory(
                PageId: id,
                PageTitle: page.Title,
                TotalEvents: history.Count,
                Events: history));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching event history for page {PageId}", id);
            return TypedResults.Problem(ex.Message, statusCode: 500);
        }
    }
}

/// <summary>
/// DTO for a single event in a page's version history.
/// </summary>
public sealed record PageEventItem(
    long Version,
    string EventType,
    DateTimeOffset Timestamp,
    string StreamKey,
    bool IsArchived);

/// <summary>
/// DTO for a page's full event history response.
/// </summary>
public sealed record PageEventHistory(
    long PageId,
    string PageTitle,
    int TotalEvents,
    IReadOnlyList<PageEventItem> Events);
