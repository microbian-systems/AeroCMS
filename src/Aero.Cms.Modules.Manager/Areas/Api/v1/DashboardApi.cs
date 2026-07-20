using Aero.Cms.Abstractions.Audit;
using Aero.Cms.Abstractions.Http.Clients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Manager.Areas.Api.v1;

/// <summary>
/// Maps administrative aggregate-count and recent-audit dashboard endpoints.
/// </summary>
/// <remarks>
/// The mapper does not attach authorization or site/tenant filters. The host must secure the
/// route group, and consumers must treat the current counts and activity as store-wide data.
/// Unexpected exception messages are copied into problem responses after logging.
/// </remarks>
public static class DashboardApi
{
    /// <summary>
    /// Maps the administrative dashboard endpoint group.
    /// </summary>
    /// <param name="app">The endpoint route builder receiving the group.</param>
    public static void MapDashboardApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/dashboard")
            .WithTags("Admin - Dashboard");

        group.MapGet("/stats", GetDashboardStats)
            .WithName("GetDashboardStats");

        group.MapGet("/activity", GetRecentActivity)
            .WithName("GetRecentActivity");
    }

    /// <summary>
    /// Counts all page and post documents in the session and returns placeholder media and user totals.
    /// </summary>
    /// <remarks>No current-site predicate is applied.</remarks>
    private static async Task<IResult> GetDashboardStats(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(DashboardApi));
        try
        {
            var totalPages = await session.Query<PageDocument>().CountAsync(cancellationToken);
            var totalBlogs = await session.Query<PostDocument>().CountAsync(cancellationToken);
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

    /// <summary>
    /// Returns the newest audit events across the store.
    /// </summary>
    /// <remarks>
    /// Event identifiers are projected as zero because the source contract exposes no identifier.
    /// The requested count is not clamped, and no current-site or tenant predicate is applied.
    /// </remarks>
    private static async Task<IResult> GetRecentActivity(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(DashboardApi));
        try
        {
            // Assuming AuditEvents are stored as a document type in AeroDB
            var activities = await session.Query<AuditEvent>()
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync(cancellationToken);

            var result = activities.Select(a => new RecentActivity(
                0, // AuditEvent doesn't have an ID in the base record, but AeroDB usually adds one or uses a property
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

