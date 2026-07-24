using Aero.Cms.Abstractions.Http.Clients;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Mvc;

namespace Aero.Cms.Modules.Content.Areas.Api.v1;

/// <summary>Maps bounded manager hierarchy reads and atomic placement mutations.</summary>
public static class ContentHierarchyManagerApi
{
    /// <summary>Maps tree, move, and exact sibling reorder endpoints.</summary>
    public static void MapContentHierarchyManagerApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/content-items")
            .WithTags("Admin - Content Hierarchy")
            .RequireAuthorization();

        group.MapGet("/{alias}/hierarchy", GetHierarchy)
            .RequireAuthorization("site:read")
            .WithName("GetContentHierarchy");
        group.MapPut("/{alias}/{id:long}/move", Move)
            .RequireAuthorization("site:update")
            .WithName("MoveContentHierarchyItem");
        group.MapPut("/{alias}/hierarchy/reorder", Reorder)
            .RequireAuthorization("site:update")
            .WithName("ReorderContentHierarchySiblings");
    }

    private static async Task<IResult> GetHierarchy(
        [FromRoute] string alias,
        [FromQuery] string? culture,
        [FromServices] ContentHierarchyManagerService service,
        CancellationToken cancellationToken)
        => ToApiResult(await service.GetTreeAsync(alias, culture, cancellationToken));

    private static async Task<IResult> Move(
        [FromRoute] string alias,
        [FromRoute] long id,
        [FromBody] MoveContentItemRequest request,
        [FromServices] ContentHierarchyManagerService service,
        CancellationToken cancellationToken)
        => ToApiResult(await service.MoveAsync(alias, id, request, cancellationToken));

    private static async Task<IResult> Reorder(
        [FromRoute] string alias,
        [FromBody] ReorderContentSiblingsRequest request,
        [FromServices] ContentHierarchyManagerService service,
        CancellationToken cancellationToken)
        => ToApiResult(await service.ReorderAsync(alias, request, cancellationToken));

    private static IResult ToApiResult<T>(Result<T> result) => result switch
    {
        Result<T>.Ok ok => TypedResults.Ok(ok.Value),
        Result<T>.Failure failure => failure.Error switch
        {
            AeroError.NotFound => TypedResults.NotFound(),
            AeroError.Conflict => TypedResults.Conflict(new { error = failure.Error.ToString() }),
            AeroError.Validation => TypedResults.BadRequest(new { error = failure.Error.ToString() }),
            _ => TypedResults.Problem(
                failure.Error.ToString(),
                statusCode: StatusCodes.Status500InternalServerError)
        },
        _ => TypedResults.Problem("Unknown content hierarchy result.")
    };
}
