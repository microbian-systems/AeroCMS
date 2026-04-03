using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Docs;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

public static class DocsApi
{
    public static void MapDocsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/docs")
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
        return result.Match<string, IReadOnlyList<DocsPage>, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> GetDocById(long id, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetByIdAsync(id, ct);
        return result.Match<string, DocsPage?, IResult>(
            ok => ok is not null ? TypedResults.Ok(ok) : (IResult)TypedResults.NotFound(),
            error => TypedResults.BadRequest(error));
    }

    private static async Task<IResult> GetDocBySlug(string slug, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetBySlugAsync(slug, ct);
        return result.Match<string, DocsPage?, IResult>(
            ok => ok is not null ? TypedResults.Ok(ok) : (IResult)TypedResults.NotFound(),
            error => TypedResults.BadRequest(error));
    }

    private static async Task<IResult> GetCategories(IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetTopLevelCategoriesAsync(ct);
        return result.Match<string, IReadOnlyList<DocsPage>, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> GetChildren(long parentId, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.GetChildrenAsync(parentId, ct);
        return result.Match<string, IReadOnlyList<DocsPage>, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> SaveDoc(DocsPage page, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.SaveAsync(page, ct);
        return result.Match<string, DocsPage, IResult>(v => TypedResults.Ok(v), e => TypedResults.BadRequest(e));
    }

    private static async Task<IResult> DeleteDoc(long id, IDocsService docsService, CancellationToken ct)
    {
        var result = await docsService.DeleteAsync(id, ct);
        return result.Match<string, bool, IResult>(_ => TypedResults.NoContent(), e => TypedResults.BadRequest(e));
    }
}
