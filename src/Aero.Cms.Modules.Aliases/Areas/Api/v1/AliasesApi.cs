using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Requests;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Aero.Cms.Modules.Aliases.Areas.Api.v1;

/// <summary>
/// Thin admin API for URL alias/redirect management.
/// Handles input validation and delegates all logic to <see cref="IAeroAliasActor"/> (Orleans grain).
/// </summary>
public static class AliasesAdminApi
{
    public static void MapAliasesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/aliases")
            .WithTags("Admin - Aliases");

        group.MapGet("/", GetAllAliases)
            .WithName("GetAllAliases");

        group.MapPost("/", CreateAlias)
            .WithName("CreateAlias");

        group.MapDelete("/{id:long}", DeleteAlias)
            .WithName("DeleteAlias");
    }

    private static async Task<IResult> GetAllAliases(
        [FromQuery] long? siteId,
        [FromServices] IAeroAliasActor aliasActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesAdminApi));
        logger.LogDebug("Getting aliases {SiteIdFilter}", siteId);
        var aliases = await aliasActor.GetAllAliasesAsync(siteId, cancellationToken);
        return TypedResults.Ok(aliases);
    }

    private static async Task<IResult> CreateAlias(
        [FromBody] CreateAliasRequest request,
        [FromServices] IValidator<CreateAliasRequest> validator,
        [FromServices] IAeroAliasActor aliasActor,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesAdminApi));

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("CreateAlias validation failed: {Errors}",
                validationResult.Errors.Select(e => e.ErrorMessage));
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        logger.LogDebug("Creating alias {OldPath} -> {NewPath}", request.OldPath, request.NewPath);
        var result = await aliasActor.CreateAsync(request, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> DeleteAlias(
        long id,
        [FromServices] IValidator<DeleteAliasRequest> validator,
        [FromServices] IAeroAliasActor aliasActor,
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

        logger.LogDebug("Deleting alias {Id}", id);
        var result = await aliasActor.DeleteAsync(deleteRequest, cancellationToken);
        return TypedResults.Ok(result);
    }
}
