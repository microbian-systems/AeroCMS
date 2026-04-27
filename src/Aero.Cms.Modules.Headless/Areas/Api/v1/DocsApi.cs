using Aero.Cms.Modules.Docs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

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
        group.MapDelete("/{id:long}", DeleteDoc);
    }

    private static async Task<IResult> ListDocs(IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetAllAsync(ct);
        return result.Match<IReadOnlyList<DocsPage>, AeroError, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> GetDocById(long id, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetByIdAsync(id, ct);
        return result.Match<DocsPage?, AeroError, IResult>(
            ok => ok is not null ? TypedResults.Ok(ok) : (IResult)TypedResults.NotFound(),
            error => TypedResults.BadRequest(error));
    }

    private static async Task<IResult> GetDocBySlug(string slug, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetBySlugAsync(slug, ct);
        return result.Match<DocsPage?, AeroError, IResult>(
            ok => ok is not null ? TypedResults.Ok(ok) : (IResult)TypedResults.NotFound(),
            error => TypedResults.BadRequest(error));
    }

    private static async Task<IResult> GetCategories(IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetTopLevelCategoriesAsync(ct);
        return result.Match<IReadOnlyList<DocsPage>, AeroError, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> GetChildren(long parentId, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetChildrenAsync(parentId, ct);
        return result.Match<IReadOnlyList<DocsPage>, AeroError, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> SaveDoc(DocsPage page, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.SaveAsync(page, ct);
        return result.Match<DocsPage, AeroError, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> DeleteDoc(long id, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.DeleteAsync(id, ct);
        return result.Match<bool, AeroError, IResult>(_ => TypedResults.NoContent(), e => TypedResults.BadRequest(e));
    }
}
