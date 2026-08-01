using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Modules.Audit.Areas.Api.v1;

/// <summary>
/// Maps the administrative endpoint that reads a unified feed from raw events
/// available through the request document session.
/// </summary>
public static class AuditApi
{
    /// <summary>
    /// Maps <c>GET /api/v1/admin/audit/</c> and names the endpoint <c>GetAuditFeed</c>.
    /// </summary>
    /// <param name="app">The route builder that receives the audit endpoint group.</param>
    /// <remarks>
    /// The route group requires the <c>AeroAdmin</c> policy because the endpoint reads raw
    /// events across the entire store without site or tenant filtering.
    /// </remarks>
public static void MapAuditApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/audit")
            .WithTags("Admin - Audit")
            .RequireAuthorization("AeroAdmin");

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
/// A projected raw-event entry returned by the audit feed.
/// </summary>
/// <param name="StreamKey">The event stream identifier, or <c>unknown</c> when the source value is absent.</param>
/// <param name="EventType">The source event type name, or <c>Unknown</c> when it is absent.</param>
/// <param name="Version">The event version within its stream.</param>
/// <param name="Timestamp">The event timestamp converted to UTC <see cref="DateTime"/>.</param>
/// <param name="IsArchived">Whether the source event payload type name ends with <c>Archived</c>.</param>
public sealed record AuditFeedItem(
    string StreamKey,
    string EventType,
    long Version,
    DateTime Timestamp,
    bool IsArchived);

/// <summary>
/// Response returned by the audit-feed endpoint.
/// </summary>
/// <param name="TotalReturned">The number of entries in <paramref name="Items"/>; this is not a total count of matching events.</param>
/// <param name="Items">The timestamp-descending entries returned after filtering and the endpoint's limit.</param>
public sealed record AuditFeedResult(
    int TotalReturned,
    IReadOnlyList<AuditFeedItem> Items);
