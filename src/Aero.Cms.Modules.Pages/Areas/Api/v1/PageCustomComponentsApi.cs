using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Pages.CustomComponents;
using Aero.Core.Http;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Pages.Areas.Api.v1;

/// <summary>
/// Represents a class for PageCustomComponentsApi.
/// </summary>
public static class PageCustomComponentsApi
{
        /// <summary>
    /// MapPageCustomComponentsApi method.
    /// </summary>
public static void MapPageCustomComponentsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(
                $"/{HttpConstants.ApiPrefix}admin/pages/custom-components")
            .WithTags("Admin - Page Custom Components");

        group.MapGet("/", GetAll)
            .WithName("GetPageCustomComponents");
        group.MapPost("/", Create)
            .WithName("CreatePageCustomComponent");
        group.MapPut("/{id:long}", Update)
            .WithName("UpdatePageCustomComponent");
        group.MapPost("/{id:long}/instance", CreateInstance)
            .WithName("CreatePageCustomComponentInstance");
        group.MapDelete("/{id:long}", Delete)
            .WithName("DeletePageCustomComponent");
    }

    private static async Task<IResult> GetAll(
        [FromServices] IPageCustomComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAllAsync(cancellationToken);
        return result switch
        {
            Result<IReadOnlyList<PageCustomComponent>, AeroError>.Ok ok =>
                Results.Ok(ok.Value.Select(ToDetail).ToList()),
            Result<IReadOnlyList<PageCustomComponent>, AeroError>.Failure failure =>
                failure.ToMinimalApiResult(),
            _ => Results.Problem("Unexpected custom component result state.")
        };
    }

    private static async Task<IResult> Create(
        [FromBody] SavePageCustomComponentRequest request,
        [FromServices] IPageCustomComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SaveAsync(request, cancellationToken);
        return MapComponentResult(result);
    }

    private static async Task<IResult> Update(
        long id,
        [FromBody] SavePageCustomComponentRequest request,
        [FromServices] IPageCustomComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return MapComponentResult(result);
    }

    private static async Task<IResult> CreateInstance(
        long id,
        [FromServices] IPageCustomComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateInstanceAsync(id, cancellationToken);
        return result.ToMinimalApiResult();
    }

    private static async Task<IResult> Delete(
        long id,
        [FromServices] IPageCustomComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.ToMinimalApiResult();
    }

    private static IResult MapComponentResult(
        Result<PageCustomComponent, AeroError> result) =>
        result switch
        {
            Result<PageCustomComponent, AeroError>.Ok ok =>
                Results.Ok(ToDetail(ok.Value)),
            Result<PageCustomComponent, AeroError>.Failure failure =>
                failure.ToMinimalApiResult(),
            _ => Results.Problem("Unexpected custom component result state.")
        };

    private static PageCustomComponentDetail ToDetail(PageCustomComponent component) =>
        new(
            component.Id,
            component.Name,
            component.Description,
            component.Category,
            component.Tags,
            component.Root,
            component.ReferencedCatalogIds,
            component.SchemaVersion,
            component.CreatedOn,
            component.ModifiedOn);

    private static IResult ToMinimalApiResult<T>(
        this Result<T, AeroError>.Failure failure) =>
        ((Result<T, AeroError>)failure).ToMinimalApiResult();
}
