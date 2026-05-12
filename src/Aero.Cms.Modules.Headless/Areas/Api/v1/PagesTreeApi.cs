using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for page hierarchy / tree operations.
/// Registered as extension method on IEndpointRouteBuilder.
/// </summary>
public static class PagesTreeApi
{
    public static void MapPagesTreeApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/pages/tree")
            .WithTags("Admin - Pages Tree");

        group.MapGet("/", GetTree)
            .WithName("GetPageTree");

        group.MapGet("/children", GetChildren)
            .WithName("GetPageChildren");

        group.MapGet("/navigation", GetNavigation)
            .WithName("GetNavigationTree");

        group.MapGet("/breadcrumb/{id:long}", GetBreadcrumb)
            .WithName("GetBreadcrumb");

        group.MapGet("/ancestors/{id:long}", GetAncestors)
            .WithName("GetAncestors");

        group.MapPut("/{id:long}/move", MovePage)
            .WithName("MovePage");

        group.MapPost("/compute-path", ComputePath)
            .WithName("ComputePath");

        group.MapGet("/next-order", GetNextOrder)
            .WithName("GetNextOrder");
    }

    private static async Task<IResult> GetTree(
        [FromServices] IPageTreeService treeService,
        [FromServices] IQuerySession query,
        CancellationToken ct)
    {
        var result = await treeService.GetTreeAsync(ct);
        return result switch
        {
            Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok =>
                Results.Ok(await MapToTreeItemsAsync(query, ok.Value, ct)),
            _ => ToApiResult(result)
        };
    }

    private static async Task<IResult> GetChildren(
        [FromServices] IPageTreeService treeService,
        [FromServices] IQuerySession query,
        [FromQuery] long? parentId,
        CancellationToken ct)
    {
        var result = await treeService.GetChildrenAsync(parentId, ct);
        return result switch
        {
            Result<IReadOnlyList<PageDocument>, AeroError>.Ok ok =>
                Results.Ok(await MapToTreeItemsAsync(query, ok.Value, ct)),
            _ => ToApiResult(result)
        };
    }

    private static async Task<IResult> GetNavigation(
        [FromServices] INavigationService navService,
        CancellationToken ct)
    {
        var result = await navService.GetNavigationTreeAsync(ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetBreadcrumb(
        [FromServices] INavigationService navService,
        [FromRoute] long id,
        CancellationToken ct)
    {
        var result = await navService.GetBreadcrumbAsync(id, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetAncestors(
        [FromServices] IPageTreeService treeService,
        [FromRoute] long id,
        CancellationToken ct)
    {
        var result = await treeService.GetAncestorsAsync(id, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> MovePage(
        [FromServices] IPageTreeService treeService,
        [FromRoute] long id,
        [FromQuery] long? newParentId,
        [FromQuery] int? order,
        CancellationToken ct)
    {
        var result = await treeService.MoveAsync(id, newParentId, order, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> ComputePath(
        [FromServices] IPageTreeService treeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? parentId,
        [FromQuery] string slug,
        [FromQuery] long? excludePageId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Results.BadRequest("Slug is required.");

        var result = await treeService.ComputePathAsync(siteContext.SiteId, parentId, slug, excludePageId, ct);
        return ToApiResult(result);
    }

    private static async Task<IResult> GetNextOrder(
        [FromServices] IPageTreeService treeService,
        [FromServices] ISiteContext siteContext,
        [FromQuery] long? parentId,
        CancellationToken ct)
    {
        var result = await treeService.GetNextSiblingOrderAsync(siteContext.SiteId, parentId, ct);
        return ToApiResult(result);
    }

    /// <summary>
    /// Converts a Result&lt;T, AeroError&gt; to an IResult for minimal API responses.
    /// </summary>
    private static IResult ToApiResult<T>(Result<T, AeroError> result)
    {
        return result switch
        {
            Result<T, AeroError>.Ok ok => Results.Ok(ok.Value),
            Result<T, AeroError>.Failure failure => failure.Error switch
            {
                AeroError.NotFound => Results.NotFound(failure.Error.ToString()),
                AeroError.Conflict => Results.Conflict(failure.Error.ToString()),
                AeroError.Validation => Results.BadRequest(failure.Error.ToString()),
                _ => Results.Problem(failure.Error.ToString(), statusCode: 500)
            },
            _ => Results.Problem("Unknown result state.")
        };
    }

    /// <summary>
    /// Maps a list of PageDocuments to tree item DTOs, resolving the
    /// <c>hasChildren</c> flag via a single batch query.
    /// </summary>
    private static async Task<List<object>> MapToTreeItemsAsync(
        IQuerySession query,
        IReadOnlyList<PageDocument> pages,
        CancellationToken ct)
    {
        var pageIds = pages.Select(p => p.Id).ToList();
        if (pageIds.Count == 0)
            return [];

        // Single batch query: find which IDs are parents of non-deleted pages
        var parentIds = await query.Query<PageDocument>()
            .Where(x => x.ParentId.HasValue
                && pageIds.Contains(x.ParentId!.Value)
                && x.Deleted == false)
            .Select(x => x.ParentId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var parentSet = parentIds.ToHashSet();

        return pages.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Slug,
            p.Path,
            p.Depth,
            p.Order,
            p.ParentId,
            PublicationState = p.PublicationState.ToString(),
            p.IsHidden,
            HasChildren = parentSet.Contains(p.Id)
        }).ToList();
    }
}
