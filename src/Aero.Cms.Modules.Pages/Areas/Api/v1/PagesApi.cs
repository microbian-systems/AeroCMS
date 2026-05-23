using System.Text.Encodings.Web;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Layout;
using Aero.Cms.Abstractions.Blocks.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Http;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core.Http;
using Aero.Cms.Web.Core.Blocks.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using GrainCreateRequest = Aero.Cms.Abstractions.Requests.CreatePageRequest;
using GrainUpdateRequest = Aero.Cms.Abstractions.Requests.UpdatePageRequest;

namespace Aero.Cms.Modules.Pages.Areas.Api.v1;

/// <summary>
/// Thin admin API for page management — delegates persistence and event sourcing
/// to <see cref="IAeroPageActor"/> (Orleans grain). Tree/navigation delegates to
/// existing services (IPageTreeService, INavigationService).
/// </summary>
public static class PagesApi
{
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

        // Draft endpoints — remain on IDocumentSession (lightweight, no grain needed)
        group.MapGet("/{id:long}/draft", GetPageDraft)
            .WithName("GetPageDraft");
        
        group.MapPut("/{id:long}/draft", SavePageDraft)
            .WithName("SavePageDraft");
        
        group.MapDelete("/{id:long}/draft", DeletePageDraft)
            .WithName("DeletePageDraft");

        // Event sourcing — version history (moved to grain)
        group.MapGet("/{id:long}/events", GetPageEvents)
            .WithName("GetPageEvents");

        // Preview endpoints (moved from Headless PreviewApi)
        app.MapGet($"/{HttpConstants.ApiPrefix}admin/preview/pages/{{id:long}}", PreviewPage)
            .WithName("PreviewPage")
            .WithTags("Admin - Preview");

        app.MapPost($"/{HttpConstants.ApiPrefix}admin/preview/pages/render-fragment", PreviewPageFragment)
            .WithName("PreviewPageFragment")
            .WithTags("Admin - Preview");
    }

    // ── Grain-backed handlers ─────────────────────────────────────────

    private static async Task<IResult> ListPages(
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var (items, totalCount) = await pagesActor.GetAllPagesAsync(skip, take, search, ct);
            var summary = items.Select(p => new PageSummary(
                p.Id, p.Title, p.Slug,
                p.CreatedOn.DateTime,
                p.PublishedOn?.DateTime,
                p.Summary)).ToList();

            return TypedResults.Ok(new PagedResult<PageSummary>(summary, totalCount, skip, take));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing pages");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetPageById(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        CancellationToken ct)
    {
        var result = await pagesActor.GetByIdAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToDetail(result.data));
    }

    private static async Task<IResult> GetPageBySlug(
        string slug,
        [FromServices] IAeroPageActor pagesActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await pagesActor.GetBySlugAsync(siteContext.SiteId, slug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(MapToDetail(result.data));
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
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var blocksJson = SerializeEditorBlocks(request.EditorBlocks);
            var layoutsJson = SerializeLayoutRegions(request.LayoutRegions);
            var grainRequest = new GrainCreateRequest(
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                null, // LayoutRegions — stripped for Orleans (transported via JSON string)
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                null, // EditorBlocks — stripped for Orleans (transported via JSON string)
                siteContext.SiteId,
                EditorBlocksJson: blocksJson,
                LayoutRegionsJson: layoutsJson);

            var result = await pagesActor.CreateAsync(grainRequest, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to create page",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Created($"/{HttpConstants.ApiPrefix}admin/pages/{result.data.Id}", MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating page");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdatePage(
        long id,
        [FromBody] Aero.Cms.Abstractions.Http.Clients.UpdatePageRequest request,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var blocksJson = SerializeEditorBlocks(request.EditorBlocks);
            var layoutsJson = SerializeLayoutRegions(request.LayoutRegions);
            var grainRequest = new GrainUpdateRequest(
                id,
                request.Title,
                request.Slug,
                request.Summary,
                request.SeoTitle,
                request.SeoDescription,
                request.PublicationState,
                request.ParentId,
                null, // LayoutRegions — stripped for Orleans (transported via JSON string)
                request.ShowInNavMenu,
                request.ShowHeaderNavigation,
                request.HideFooter,
                request.ShowChatAgent,
                null, // EditorBlocks — stripped for Orleans (transported via JSON string)
                EditorBlocksJson: blocksJson,
                LayoutRegionsJson: layoutsJson);

            var result = await pagesActor.UpdateAsync(grainRequest, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Failed to update page",
                    Detail = result.error.Message,
                    Status = StatusCodes.Status400BadRequest
                })
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeletePage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        CancellationToken ct)
    {
        var result = await pagesActor.DeleteAsync(new DeletePageRequest(id), ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(true);
    }

    private static async Task<IResult> DeletePageCascade(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        CancellationToken ct)
    {
        var result = await pagesActor.DeleteAsync(new DeletePageRequest(id), ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(true);
    }

    private static async Task<IResult> DeleteMultiplePages(
        [FromBody] DeleteMultiplePagesRequest request,
        [FromServices] IAeroPageActor pagesActor,
        CancellationToken ct)
    {
        var count = await pagesActor.DeleteMultipleAsync(request.Ids.ToArray(), request.DeleteDescendants, ct);
        return TypedResults.Ok(new { deleted = count });
    }

    private static async Task<IResult> PublishPage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pagesActor.PublishAsync(id, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UnpublishPage(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pagesActor.UnpublishAsync(id, ct);
            return !string.IsNullOrWhiteSpace(result.error.Message)
                ? TypedResults.NotFound(result.error)
                : TypedResults.Ok(MapToDetail(result.data));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unpublishing page {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    // ── Draft handlers (lightweight, stay on session) ─────────────────

    private static async Task<IResult> GetPageDraft(
        long id, IQuerySession querySession)
    {
        var draft = await querySession.Query<PageDraft>()
            .FirstOrDefaultAsync(d => d.PageId == id);
        return TypedResults.Ok(draft);
    }

    private static async Task<IResult> SavePageDraft(
        long id,
        PageDraftRequest request,
        IDocumentSession session,
        IQuerySession querySession)
    {
        var page = await querySession.LoadAsync<PageDocument>(id);
        if (page is null) return TypedResults.NotFound();

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

    // ── Event sourcing — version history (grain-backed) ───────────────

    private static async Task<IResult> GetPageEvents(
        long id,
        [FromServices] IAeroPageActor pagesActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var events = await pagesActor.GetEventHistoryAsync(id, ct);
            if (events.Count == 0)
                return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });

            return TypedResults.Ok(events);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching event history for page {PageId}", id);
            return TypedResults.Problem(ex.Message, statusCode: 500);
        }
    }

    // ── Mapping helpers ────────────────────────────────────────────────

    private static PageDetail MapToDetail(PageViewModel vm)
    {
        var blocks = DeserializeEditorBlockList(vm.EditorBlocksJson);

        return new PageDetail(
            vm.Id,
            vm.Title ?? "",
            vm.Slug ?? "",
            vm.Summary,
            vm.SeoTitle,
            vm.SeoDescription,
            vm.CreatedOn.DateTime,
            (vm.ModifiedOn ?? vm.CreatedOn).DateTime,
            vm.PublishedOn?.DateTime,
            vm.IsPublished ? ContentPublicationState.Published : ContentPublicationState.Draft,
            blocks?.Count ?? 0,
            vm.ShowInNavMenu,
            vm.ShowHeaderNavigation,
            vm.HideFooter,
            vm.ShowChatAgent,
            blocks,
            vm.ParentId,
            vm.Path ?? "",
            vm.Depth
        );
    }

    private static List<EditorBlock>? DeserializeEditorBlockList(string? json)
    {
        if (json is null)
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<List<EditorBlock>>(
            json, BlockJsonContext.Default.Options);
    }

    // ── Preview handlers (moved from Headless PreviewApi) ──────────────

    private static async Task<IResult> PreviewPage(
        long id,
        IPageContentService pageService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            var result = await pageService.LoadAsync(id, ct);
            if (result is Result<PageDocument?, AeroError>.Ok { Value: not null } ok)
                return TypedResults.Ok(new PreviewResponse<PageDocument>(ok.Value, "page"));

            return TypedResults.NotFound(new { error = $"Page with id '{id}' not found." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error previewing page {Id}", id);
            return TypedResults.Json(new { error = "An error occurred processing your request." }, statusCode: 500);
        }
    }

    private static async Task<IResult> PreviewPageFragment(
        [FromBody] PreviewPageFragmentRequest request,
        CmsBlockHtmlRenderer blockRenderer,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(typeof(PagesApi));
        try
        {
            if ((request.Blocks is null || request.Blocks.Count == 0) &&
                (request.LayoutRegions is null || request.LayoutRegions.Count == 0))
                return TypedResults.BadRequest(new { error = "Page blocks or layout regions are required." });

            var html = request.Blocks is { Count: > 0 }
                ? await blockRenderer.RenderBlocksAsync(EditorBlockMapper.MapBlocks(request.Blocks), cancellationToken: ct)
                : await blockRenderer.RenderRegionsAsync(request.LayoutRegions ?? [], ct);

            return TypedResults.Ok(new PreviewPageFragmentResponse(RenderPreviewHtml(html)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rendering preview page fragment");
            return TypedResults.Json(new { error = "An error occurred rendering the preview fragment." }, statusCode: 500);
        }
    }

    // ── Preview helpers ─────────────────────────────────────────────────

    private static string RenderPreviewHtml(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    // ── Orleans transport helpers ───────────────────────────────────────

    /// <summary>
    /// Serializes <see cref="EditorBlock"/> list to JSON for Orleans-safe grain transport.
    /// Returns null when blocks are omitted (preserve existing), string when blocks are provided.
    /// Empty list serializes to "[]" (clear blocks).
    /// </summary>
    private static string? SerializeEditorBlocks(IReadOnlyList<EditorBlock>? blocks)
    {
        if (blocks is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(
            blocks,
            BlockJsonContext.Default.Options);
    }

    private static string? SerializeLayoutRegions(IReadOnlyList<LayoutRegion>? regions)
    {
        if (regions is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(
            regions,
            BlockJsonContext.Default.Options);
    }
}
