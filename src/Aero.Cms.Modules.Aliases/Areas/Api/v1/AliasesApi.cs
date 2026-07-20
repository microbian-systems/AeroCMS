using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Requests;
using Aero.Core.Http;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Aliases.Areas.Api.v1;

/// <summary>
/// Maps the administrative HTTP API for listing, creating, and deleting aliases.
/// Endpoint handlers validate create and delete requests and delegate persistence
/// to <see cref="IAeroAliasActor"/>. Every operation is authorized and scoped
/// to the request's selected site.
/// </summary>
public static class AliasesAdminApi
{
    /// <summary>
    /// Maps <c>GET</c>, <c>POST</c>, and <c>DELETE</c> endpoints under
    /// <c>/api/v1/admin/aliases</c>. Listing is selected-site scoped; create
    /// returns the created alias and delete returns no content.
    /// </summary>
public static void MapAliasesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/aliases")
            .WithTags("Admin - Aliases")
            .RequireAuthorization();

        group.MapGet("/", GetAllAliases)
            .RequireAuthorization("site:read")
            .WithName("GetAllAliases");

        group.MapPost("/", CreateAlias)
            .RequireAuthorization("site:create")
            .WithName("CreateAlias");

        group.MapDelete("/{id:long}", DeleteAlias)
            .RequireAuthorization("site:delete")
            .WithName("DeleteAlias");
    }

    private static async Task<IResult> GetAllAliases(
        [FromServices] IAeroAliasActor aliasActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesAdminApi));
        logger.LogDebug("Getting aliases for selected site {SiteId}", siteContext.SiteId);
        var aliases = await aliasActor.GetAllAliasesAsync(siteContext.SiteId, cancellationToken);
        return TypedResults.Ok(aliases);
    }

    private static async Task<IResult> CreateAlias(
        [FromBody] CreateAliasRequest request,
        [FromServices] IValidator<CreateAliasRequest> validator,
        [FromServices] IAeroAliasActor aliasActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesAdminApi));
        var selectedSiteRequest = request with { SiteId = siteContext.SiteId };

        var validationResult = await validator.ValidateAsync(selectedSiteRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateAlias validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating alias {OldPath} -> {NewPath}", request.OldPath, request.NewPath);
        var result = await aliasActor.CreateAliasAsync(selectedSiteRequest, siteContext.SiteId, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.BadRequest(result.error)
            : TypedResults.Ok(result.data);
    }

    private static async Task<IResult> DeleteAlias(
        long id,
        [FromServices] IValidator<DeleteAliasRequest> validator,
        [FromServices] IAeroAliasActor aliasActor,
        [FromServices] ISiteContext siteContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesAdminApi));
        var deleteRequest = new DeleteAliasRequest(id);

        var validationResult = await validator.ValidateAsync(deleteRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("DeleteAlias validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var existing = await aliasActor.GetByIdAsync(id, siteContext.SiteId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing.error.Message))
            return TypedResults.NotFound(existing.error);

        logger.LogDebug("Deleting alias {Id} from site {SiteId}", id, siteContext.SiteId);
        var result = await aliasActor.DeleteAliasAsync(id, siteContext.SiteId, cancellationToken);
        return !string.IsNullOrWhiteSpace(result.error.Message)
            ? TypedResults.NotFound(result.error)
            : TypedResults.NoContent();
    }
}
