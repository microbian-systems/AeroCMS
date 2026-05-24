using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Docs.Areas.Api.v1;

/// <summary>
/// Thin admin API for docs management — delegates persistence to <see cref="IAeroDocsActor"/> (Orleans grain).
/// </summary>
public static class DocsApi
{
    public static void MapDocsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/docs")
            .WithTags("Admin - Docs");

        group.MapGet("/", ListDocs);
        group.MapGet("/{id:long}", GetDocById);
        group.MapGet("/by-slug/{*slug}", GetDocBySlug);
        group.MapGet("/categories", GetCategories);
        group.MapGet("/{parentId:long}/children", GetChildren);
        group.MapPost("/", SaveDoc);
        group.MapPost("/{spaceId:long}/sections/{parentId:long}/children", CreateChildSection);
        group.MapPost("/{spaceId:long}/sections/{id:long}/move", MoveSection);
        group.MapPost("/{spaceId:long}/sections/reorder", ReorderSections);
        group.MapPost("/{id:long}/publish", PublishDoc);
        group.MapPost("/{id:long}/unpublish", UnpublishDoc);
        group.MapDelete("/{id:long}", DeleteDoc);
    }

    private static async Task<IResult> ListDocs(
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var docs = await docsActor.GetAllBySiteAsync(siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    private static async Task<IResult> GetDocById(
        long id,
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.GetByIdAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> GetDocBySlug(
        string slug,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.GetBySlugAsync(siteContext.SiteId, slug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> GetCategories(
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var docs = await docsActor.GetTopLevelCategoriesAsync(siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    private static async Task<IResult> GetChildren(
        long parentId,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var docs = await docsActor.GetChildrenAsync(parentId, siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    private static async Task<IResult> SaveDoc(
        [FromBody] DocViewModel vm,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        vm.SiteId = siteContext.SiteId;
        var result = await docsActor.SaveAsync(vm, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> DeleteDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.DeleteAsync(new DeleteDocRequest(id), ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.NoContent();
    }

    private static async Task<IResult> CreateChildSection(
        long spaceId,
        long parentId,
        [FromBody] DocsCreateChildRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.CreateChildSectionAsync(siteContext.SiteId, spaceId, parentId, request.Title, request.Summary, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> MoveSection(
        long spaceId,
        long id,
        [FromBody] DocsMoveRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.MoveSectionAsync(siteContext.SiteId, spaceId, id, request.NewParentId, request.Order, request.RewriteSlug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> ReorderSections(
        long spaceId,
        [FromBody] DocsReorderRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.ReorderSectionsAsync(siteContext.SiteId, spaceId, request.ParentId, request.OrderedIds, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(true);
    }

    private static async Task<IResult> PublishDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.PublishAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> UnpublishDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.UnpublishAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }
}
