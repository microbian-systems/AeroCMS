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
/// Maps the minimal HTTP endpoints used to administer documentation pages.
/// </summary>
/// <remarks>
/// Handlers delegate to <see cref="IAeroDocsActor"/> and translate its error model into HTTP
/// results. The route group requires an authenticated principal and every identifier-bearing
/// operation is constrained to the selected site.
/// </remarks>
public static class DocsApi
{
    /// <summary>
    /// Maps documentation CRUD, translation, hierarchy, and publication endpoints.
    /// </summary>
    /// <param name="app">The route builder that receives the admin route group.</param>
    /// <remarks>
    /// Routes are added beneath <c>/{api-prefix}admin/docs</c> and require authentication.
    /// </remarks>
public static void MapDocsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/docs")
            .WithTags("Admin - Docs")
            .RequireAuthorization();

        group.MapGet("/", ListDocs).RequireAuthorization("site:read");
        group.MapGet("/{id:long}", GetDocById).RequireAuthorization("site:read");
        group.MapGet("/by-slug/{*slug}", GetDocBySlug).RequireAuthorization("site:read");
        group.MapGet("/categories", GetCategories).RequireAuthorization("site:read");
        group.MapGet("/{parentId:long}/children", GetChildren).RequireAuthorization("site:read");
        group.MapGet("/{id:long}/translations", ListTranslations).RequireAuthorization("site:read");
        group.MapPost("/{id:long}/translations", ForkToCulture).RequireAuthorization("site:create");
        group.MapPost("/", CreateDoc).RequireAuthorization("site:create");
        group.MapPut("/{id:long}", UpdateDoc).RequireAuthorization("site:update");
        group.MapPost("/{spaceId:long}/sections/{parentId:long}/children", CreateChildSection).RequireAuthorization("site:create");
        group.MapPost("/{spaceId:long}/sections/{id:long}/move", MoveSection).RequireAuthorization("site:update");
        group.MapPost("/{spaceId:long}/sections/reorder", ReorderSections).RequireAuthorization("site:update");
        group.MapPost("/{id:long}/publish", PublishDoc).RequireAuthorization("site:update");
        group.MapPost("/{id:long}/unpublish", UnpublishDoc).RequireAuthorization("site:update");
        group.MapDelete("/{id:long}", DeleteDoc).RequireAuthorization("site:delete");
    }

    /// <summary>
    /// Returns all pages for the current site, including drafts and every culture.
    /// </summary>
    private static async Task<IResult> ListDocs(
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var docs = await docsActor.GetAllBySiteAsync(siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    /// <summary>
    /// Loads a page by identifier without applying the request's current-site context.
    /// </summary>
    private static async Task<IResult> GetDocById(
        long id,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.GetByIdAsync(id, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Loads an exact slug within the request's current site, including unpublished pages.
    /// </summary>
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

    /// <summary>
    /// Returns direct children of the current site's virtual docs root.
    /// </summary>
    private static async Task<IResult> GetCategories(
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var docs = await docsActor.GetTopLevelCategoriesAsync(siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    /// <summary>
    /// Returns children for the actor execution context's UI culture within the current site.
    /// </summary>
    private static async Task<IResult> GetChildren(
        long parentId,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        var parent = await docsActor.GetByIdAsync(parentId, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(parent.error.Message)) return TypedResults.NotFound(parent.error);
        var docs = await docsActor.GetChildrenAsync(parentId, siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    /// <summary>
    /// Returns culture variants after an identifier-only load derives the stored site.
    /// The actor does not compare that site with the current request site; the host must
    /// authorize access to the supplied identifier.
    /// </summary>
    private static async Task<IResult> ListTranslations(
        long id,
        [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext,
        CancellationToken ct)
    {
        var source = await docsActor.GetByIdAsync(id, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(source.error.Message)) return TypedResults.NotFound(source.error);
        var docs = await docsActor.ListCultureVariantsAsync(id, siteContext.SiteId, ct);
        return TypedResults.Ok(docs);
    }

    /// <summary>
    /// Creates a draft translation after an identifier-only load derives the source site.
    /// The actor does not compare that site with the current request site; the host must
    /// authorize access to the supplied identifier.
    /// </summary>
    private static async Task<IResult> ForkToCulture(
        long id,
        [FromBody] ForkDocsCultureRequest request,
        [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext,
        CancellationToken ct)
    {
        var source = await docsActor.GetByIdAsync(id, siteContext.SiteId, ct);
        if (!string.IsNullOrWhiteSpace(source.error.Message)) return TypedResults.NotFound(source.error);
        var result = await docsActor.ForkDocForCultureAsync(id, siteContext.SiteId, request.Culture, request.Slug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Creates a new draft in the selected site. The submitted site and publication fields are not authoritative.
    /// </summary>
    private static async Task<IResult> CreateDoc(
        [FromBody] DocViewModel vm,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        if (vm.Id != 0) return TypedResults.BadRequest(new DocErrorViewModel { Message = "A new doc must not specify an identifier" });
        var result = await docsActor.SaveAsync(vm, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> UpdateDoc(long id, [FromBody] DocViewModel vm, [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext, CancellationToken ct)
    {
        if (vm.Id != id) return TypedResults.BadRequest(new DocErrorViewModel { Message = "Route and body doc identifiers must match" });
        if (!await Exists(docsActor, id, siteContext.SiteId, ct)) return TypedResults.NotFound();
        var result = await docsActor.SaveAsync(vm, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message) ? TypedResults.BadRequest(result.error) : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Deletes a page after an identifier-only load derives its stored site. The actor does not
    /// compare that site with the current request site; the host must authorize the identifier.
    /// </summary>
    private static async Task<IResult> DeleteDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.DeleteDocAsync(id, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.NoContent();
    }

    /// <summary>
    /// Creates a child within the request site's selected docs space.
    /// </summary>
    private static async Task<IResult> CreateChildSection(
        long spaceId,
        long parentId,
        [FromBody] DocsCreateChildRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!await Exists(docsActor, spaceId, siteContext.SiteId, ct) || !await Exists(docsActor, parentId, siteContext.SiteId, ct)) return TypedResults.NotFound();
        var result = await docsActor.CreateChildSectionAsync(siteContext.SiteId, spaceId, parentId, request.Title, request.Summary, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Moves a section within the request site's selected docs space.
    /// </summary>
    private static async Task<IResult> MoveSection(
        long spaceId,
        long id,
        [FromBody] DocsMoveRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!await Exists(docsActor, spaceId, siteContext.SiteId, ct) || !await Exists(docsActor, id, siteContext.SiteId, ct) || !await Exists(docsActor, request.NewParentId, siteContext.SiteId, ct)) return TypedResults.NotFound();
        var result = await docsActor.MoveSectionAsync(siteContext.SiteId, spaceId, id, request.NewParentId, request.Order, request.RewriteSlug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Reorders selected siblings within the request site's selected docs space.
    /// </summary>
    private static async Task<IResult> ReorderSections(
        long spaceId,
        [FromBody] DocsReorderRequest request,
        [FromServices] IAeroDocsActor docsActor,
        ISiteContext siteContext,
        CancellationToken ct)
    {
        if (!await Exists(docsActor, spaceId, siteContext.SiteId, ct) || !await Exists(docsActor, request.ParentId, siteContext.SiteId, ct) || !await AllExist(docsActor, request.OrderedIds, siteContext.SiteId, ct)) return TypedResults.NotFound();
        var result = await docsActor.ReorderSectionsAsync(siteContext.SiteId, spaceId, request.ParentId, request.OrderedIds, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(true);
    }

    /// <summary>
    /// Publishes a page after an identifier-only load derives its stored site. The actor does not
    /// compare that site with the current request site; the host must authorize the identifier.
    /// </summary>
    private static async Task<IResult> PublishDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.PublishAsync(id, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Returns a page to draft after an identifier-only load derives its stored site. The actor
    /// does not compare that site with the current request site; the host must authorize the identifier.
    /// </summary>
    private static async Task<IResult> UnpublishDoc(
        long id,
        [FromServices] IAeroDocsActor docsActor, ISiteContext siteContext,
        CancellationToken ct)
    {
        var result = await docsActor.UnpublishAsync(id, siteContext.SiteId, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<bool> Exists(IAeroDocsActor actor, long id, long siteId, CancellationToken ct)
        => string.IsNullOrWhiteSpace((await actor.GetByIdAsync(id, siteId, ct)).error.Message);
    private static async Task<bool> AllExist(IAeroDocsActor actor, IEnumerable<long> ids, long siteId, CancellationToken ct)
    { foreach (var id in ids.Distinct()) if (!await Exists(actor, id, siteId, ct)) return false; return true; }
}
