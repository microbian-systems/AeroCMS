using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Modules.Audit.Areas.Api.v1;

/// <summary>
/// Admin API for the global audit feed. Queries the AeroDB event store
/// (<c>mt_events</c>) across all streams to produce a unified activity
/// timeline.  Per-document version history is handled separately via
/// <c>GET /admin/pages/{id}/events</c> (see <see cref="PagesApi"/>).
/// </summary>
public static class AuditApi
{
    public static void MapAuditApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/audit")
            .WithTags("Admin - Audit");

        group.MapGet("/", GetAuditFeed)
            .WithName("GetAuditFeed");
    }

    private static async Task<IResult> GetAuditFeed(
        [FromServices] IDocumentSession session,
        [FromServices] ILoggerFactory loggerFactory,
        [FromQuery] string? type,       // "Page", "BlogPost", etc. — filters by stream key prefix
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var logger = loggerFactory.CreateLogger(typeof(AuditApi));
        try
        {
            var query = session.Events.QueryAllRawEvents();

            if (from.HasValue)
                query = query.Where(e => e.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.Timestamp <= to.Value);

            if (!string.IsNullOrWhiteSpace(type))
            {
                var prefix = type.ToLowerInvariant() switch
                {
                    "page" => "page-",
                    "blogpost" or "blog" => "blog-",
                    _ => type.ToLowerInvariant() + "-"
                };
                query = query.Where(e => e.StreamId.Value!.StartsWith(prefix));
            }

            var events = await query
                .OrderByDescending(e => e.Timestamp)
                .Take(Math.Min(take, 200))
                .ToListAsync(ct);

            var feed = events.Select(e => new AuditFeedItem(
                StreamKey: e.StreamId.Value ?? "unknown",
                EventType: e.EventType.Name ?? "Unknown",
                Version: e.Version,
                Timestamp: e.Timestamp.UtcDateTime,
                IsArchived: e.Data.GetType().Name.EndsWith("Archived")
            )).ToList();

            return TypedResults.Ok(new AuditFeedResult(
                TotalReturned: feed.Count,
                Items: feed));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query global audit feed");
            return TypedResults.Problem(ex.Message, statusCode: 500);
        }
    }
}

/// <summary>
/// A single item in the global audit activity feed.
/// </summary>
public sealed record AuditFeedItem(
    string StreamKey,
    string EventType,
    long Version,
    DateTime Timestamp,
    bool IsArchived);

/// <summary>
/// Response wrapper for the global audit feed.
/// </summary>
public sealed record AuditFeedResult(
    int TotalReturned,
    IReadOnlyList<AuditFeedItem> Items);
