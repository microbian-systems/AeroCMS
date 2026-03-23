namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for dashboard endpoints (stub implementation).
/// </summary>
public class DashboardClient : AeroClientBase
{
    protected override string ResourceName => "dashboard";

    public DashboardClient(HttpClient httpClient, ILogger<DashboardClient> logger)
        : base(httpClient, logger)
    {
    }

    public Task<DashboardStats?> GetStatsAsync(CancellationToken ct = default)
    {
        return GetAsync<DashboardStats>("stats", ct);
    }

    public Task<IReadOnlyList<RecentActivity>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<RecentActivity>>($"activity?count={count}", ct) 
            ?? Task.FromResult<IReadOnlyList<RecentActivity>>(Array.Empty<RecentActivity>());
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record DashboardStats(int TotalPages, int TotalBlogs, int TotalMedia, int TotalUsers, DateTime? LastUpdated);
public record RecentActivity(long Id, string Action, string EntityType, long EntityId, string EntityTitle, DateTime Timestamp, long UserId);
