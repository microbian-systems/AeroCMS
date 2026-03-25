namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IDashboardHttpClient
{
    Task<Result<string, DashboardStats>> GetStatsAsync(CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<RecentActivity>>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default);
}

/// <summary>
/// Typed client for dashboard endpoints (stub implementation).
/// </summary>
public class DashboardHttpClient(HttpClient httpClient, ILogger<DashboardHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IDashboardHttpClient
{
    protected override string ResourceName => "dashboard";

    public Task<Result<string, DashboardStats>> GetStatsAsync(CancellationToken ct = default)
    {
        return GetResultAsync<DashboardStats>("stats", ct);
    }

    public Task<Result<string, IReadOnlyList<RecentActivity>>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<RecentActivity>>($"activity?count={count}", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record DashboardStats(int TotalPages, int TotalBlogs, int TotalMedia, int TotalUsers, DateTime? LastUpdated);
public record RecentActivity(long Id, string Action, string EntityType, long EntityId, string EntityTitle, DateTime Timestamp, long UserId);
