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
/// results. The route group does not attach authorization metadata. Some identifier-based
/// handlers also do not compare the loaded page with the request's current site, so the host
/// must enforce authorization and site ownership before these endpoints are exposed.
/// </remarks>
public static class DocsApi
{
    /// <summary>
    /// Maps documentation CRUD, translation, hierarchy, and publication endpoints.
    /// </summary>
    /// <param name="app">The route builder that receives the admin route group.</param>
    /// <remarks>
    /// Routes are added beneath <c>/{api-prefix}admin/docs</c>. This method does not call
    /// <c>RequireAuthorization</c>; host-level conventions or middleware must protect the group.
    /// </remarks>
public static void MapDocsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/docs")
            .WithTags("Admin - Docs");

        group.MapGet("/", ListDocs);
        group.MapGet("/{id:long}", GetDocById);
        group.MapGet("/by-slug/{*slug}", GetDocBySlug);
        group.MapGet("/categories", GetCategories);
        group.MapGet("/{parentId:long}/children", GetChildren);
        group.MapGet("/{id:long}/translations", ListTranslations);
        group.MapPost("/{id:long}/translations", ForkToCulture);
        group.MapPost("/", SaveDoc);
        group.MapPost("/{spaceId:long}/sections/{parentId:long}/children", CreateChildSection);
        group.MapPost("/{spaceId:long}/sections/{id:long}/move", MoveSection);
        group.MapPost("/{spaceId:long}/sections/reorder", ReorderSections);
        group.MapPost("/{id:long}/publish", PublishDoc);
        group.MapPost("/{id:long}/unpublish", UnpublishDoc);
        group.MapDelete("/{id:long}", DeleteDoc);
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
        CancellationToken ct)
    {
        var result = await docsActor.GetByIdAsync(id, ct);
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
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var docs = await docsActor.ListCultureVariantsAsync(id, ct);
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
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.ForkDocForCultureAsync(id, request.Culture, request.Slug, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Overwrites the submitted site identifier with the request site and saves the view model.
    /// For an existing identifier, the actor loads the document without first comparing its
    /// stored site to the request site; callers must verify ownership before saving because the
    /// service subsequently applies the request site.
    /// </summary>
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

    /// <summary>
    /// Deletes a page after an identifier-only load derives its stored site. The actor does not
    /// compare that site with the current request site; the host must authorize the identifier.
    /// </summary>
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
        [FromServices] IAeroDocsActor docsActor,
        CancellationToken ct)
    {
        var result = await docsActor.PublishAsync(id, ct);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    /// <summary>
    /// Returns a page to draft after an identifier-only load derives its stored site. The actor
    /// does not compare that site with the current request site; the host must authorize the identifier.
    /// </summary>
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
