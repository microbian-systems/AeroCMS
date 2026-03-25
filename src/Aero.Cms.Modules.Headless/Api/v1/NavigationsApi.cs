using Aero.Cms.Core;
using Aero.Cms.Core.Http.Clients;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for navigation management.
/// </summary>
public static class NavigationsApi
{
    /// <summary>
    /// Maps the Navigations Admin API endpoints.
    /// </summary>
    public static void MapNavigationsApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/navigations")
            .WithTags("Admin - Navigations");

        group.MapGet("/", GetAllNavigations)
            .WithName("GetAllNavigations");

        group.MapGet("/details/{id:long}", GetNavigationById)
            .WithName("GetNavigationById");

        group.MapPost("/", CreateNavigation)
            .WithName("CreateNavigation");

        group.MapPut("/{id:long}", UpdateNavigation)
            .WithName("UpdateNavigation");

        group.MapDelete("/{id:long}", DeleteNavigation)
            .WithName("DeleteNavigation");
    }

    private static async Task<IResult> GetAllNavigations(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(NavigationsApi));
        try
        {
            var navigations = await session.Query<Navigation>()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            var summaries = navigations.Select(n => new NavigationSummary(
                n.Id,
                n.Name,
                n.Location,
                n.Items.Count,
                n.CreatedOn.DateTime
            )).ToList();

            return TypedResults.Ok(summaries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving all navigations");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetNavigationById(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(NavigationsApi));
        try
        {
            var navigation = await session.LoadAsync<Navigation>(id, cancellationToken);

            if (navigation is null)
            {
                return TypedResults.NotFound(new { error = $"Navigation with ID {id} not found." });
            }

            var detail = new NavigationDetail(
                navigation.Id,
                navigation.Name,
                navigation.Location,
                navigation.Items.Select(i => new NavigationItemDetail(
                    i.Id,
                    i.Label,
                    i.Url,
                    i.PageId,
                    i.Order,
                    i.ParentId
                )).ToList(),
                navigation.CreatedOn.DateTime,
                navigation.ModifiedOn.GetValueOrDefault().DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving navigation for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> CreateNavigation(
        [FromBody] CreateNavigationRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(NavigationsApi));
        try
        {
            var navigation = new Navigation
            {
                Id = Snowflake.NewId(),
                Name = request.Name,
                Location = request.Location,
                Items = request.Items.Select(i => new NavigationItem
                {
                    Id = Snowflake.NewId(),
                    Label = i.Label,
                    Url = i.Url,
                    PageId = i.PageId,
                    Order = i.Order,
                    ParentId = i.ParentId
                }).ToList()
            };

            session.Store(navigation);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new NavigationDetail(
                navigation.Id,
                navigation.Name,
                navigation.Location,
                navigation.Items.Select(i => new NavigationItemDetail(
                    i.Id,
                    i.Label,
                    i.Url,
                    i.PageId,
                    i.Order,
                    i.ParentId
                )).ToList(),
                navigation.CreatedOn.DateTime,
                navigation.ModifiedOn.GetValueOrDefault().DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating navigation");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateNavigation(
        long id,
        [FromBody] UpdateNavigationRequest request,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(NavigationsApi));
        try
        {
            var navigation = await session.LoadAsync<Navigation>(id, cancellationToken);

            if (navigation is null)
            {
                return TypedResults.NotFound(new { error = $"Navigation with ID {id} not found." });
            }

            navigation.Name = request.Name;
            navigation.Location = request.Location;
            navigation.Items = request.Items.Select(i => new NavigationItem
            {
                Id = i.Id == 0 ? Snowflake.NewId() : i.Id,
                Label = i.Label,
                Url = i.Url,
                PageId = i.PageId,
                Order = i.Order,
                ParentId = i.ParentId
            }).ToList();

            session.Store(navigation);
            await session.SaveChangesAsync(cancellationToken);

            var detail = new NavigationDetail(
                navigation.Id,
                navigation.Name,
                navigation.Location,
                navigation.Items.Select(i => new NavigationItemDetail(
                    i.Id,
                    i.Label,
                    i.Url,
                    i.PageId,
                    i.Order,
                    i.ParentId
                )).ToList(),
                navigation.CreatedOn.DateTime,
                navigation.ModifiedOn.GetValueOrDefault().DateTime
            );

            return TypedResults.Ok(detail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating navigation for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> DeleteNavigation(
        long id,
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(NavigationsApi));
        try
        {
            var navigation = await session.LoadAsync<Navigation>(id, cancellationToken);

            if (navigation is null)
            {
                return TypedResults.NotFound(new { error = $"Navigation with ID {id} not found." });
            }

            session.Delete(navigation);
            await session.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting navigation for id={Id}", id);
            return TypedResults.Problem(ex.Message);
        }
    }
}
