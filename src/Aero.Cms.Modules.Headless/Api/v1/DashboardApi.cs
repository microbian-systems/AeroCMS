using Aero.Cms.Core.Audit;
using Aero.Cms.Core.Entities;
using Aero.Cms.Abstractions.Http.Clients;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for dashboard statistics and activity.
/// </summary>
public static class DashboardApi
{
    /// <summary>
    /// Maps the Dashboard Admin API endpoints.
    /// </summary>
    public static void MapDashboardApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/dashboard")
            .WithTags("Admin - Dashboard");

        group.MapGet("/stats", GetDashboardStats)
            .WithName("GetDashboardStats");

        group.MapGet("/activity", GetRecentActivity)
            .WithName("GetRecentActivity");
    }

    private static async Task<IResult> GetDashboardStats(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(DashboardApi));
        try
        {
            var totalPages = await session.Query<PageDocument>().CountAsync(cancellationToken);
            var totalBlogs = await session.Query<BlogPostDocument>().CountAsync(cancellationToken);
            // var totalMedia = await session.Query<MediaDocument>().CountAsync(cancellationToken);
            // var totalUsers = await session.Query<UserDocument>().CountAsync(cancellationToken);

            var stats = new DashboardStats(
                totalPages,
                totalBlogs,
                0, // TODO: totalMedia
                0, // TODO: totalUsers
                DateTime.UtcNow
            );

            return TypedResults.Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving dashboard stats");
            return TypedResults.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetRecentActivity(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(DashboardApi));
        try
        {
            // Assuming AuditEvents are stored as a document type in Marten
            var activities = await session.Query<AuditEvent>()
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync(cancellationToken);

            var result = activities.Select(a => new RecentActivity(
                0, // AuditEvent doesn't have an ID in the base record, but Marten usually adds one or uses a property
                a.EventType,
                a.EntityType,
                a.EntityId,
                a.Metadata?.GetValueOrDefault("Title") ?? "Unknown",
                a.Timestamp.DateTime,
                a.UserId
            )).ToList();

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving recent activity");
            return TypedResults.Problem(ex.Message);
        }
    }
}

