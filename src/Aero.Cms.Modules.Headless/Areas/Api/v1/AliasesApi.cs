using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Aliases;
using Aero.Core;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Aero.Cms.Modules.Headless.Areas.Api.v1;

/// <summary>
/// Admin API for URL alias/redirect management.
/// </summary>
public static class AliasesApi
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
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesApi));
        try
        {
            IQueryable<AliasDocument> query = session.Query<AliasDocument>();

            if (siteId.HasValue)
            {
                query = query.Where(x => x.SiteId == siteId.Value);
            }

            var aliases = await query.OrderBy(x => x.OldPath).ToListAsync(cancellationToken);

            var models = aliases.Select(a => new AliasViewModel
            {
                Id = a.Id,
                SiteId = a.SiteId,
                OldPath = a.OldPath,
                NewPath = a.NewPath,
                Notes = a.Notes,
                CreatedOn = a.CreatedOn,
                ModifiedOn = a.ModifiedOn,
                CreatedBy = a.CreatedBy,
                ModifiedBy = a.ModifiedBy
            }).ToList();

            return TypedResults.Ok(models);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving aliases");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> CreateAlias(
        [FromBody] CreateAliasRequest request,
        [FromServices] IAliasService aliasService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesApi));
        try
        {
            var document = new AliasDocument
            {
                Id = Snowflake.NewId(),
                SiteId = request.SiteId,
                OldPath = request.OldPath,
                NewPath = request.NewPath,
                Notes = request.Notes
            };

            await aliasService.CreateAsync(document, cancellationToken);

            var model = new AliasViewModel
            {
                Id = document.Id,
                SiteId = document.SiteId,
                OldPath = document.OldPath,
                NewPath = document.NewPath,
                Notes = document.Notes,
                CreatedOn = document.CreatedOn,
                CreatedBy = document.CreatedBy
            };

            return TypedResults.Ok(model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating alias");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteAlias(
        long id,
        [FromServices] IAliasService aliasService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AliasesApi));
        try
        {
            await aliasService.DeleteAsync(id, cancellationToken);
            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting alias {Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
