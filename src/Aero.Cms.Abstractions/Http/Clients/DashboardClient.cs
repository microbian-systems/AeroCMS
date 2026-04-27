namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for dashboard HTTP client.
/// </summary>
public interface IDashboardHttpClient
{
    /// <summary>
    /// Gets dashboard statistics.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the dashboard stats or an error.</returns>
    Task<Result<DashboardStats, AeroError>> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets recent activity list.
    /// </summary>
    /// <param name="count">The maximum number of items to return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the list of recent activity or an error.</returns>
    Task<Result<IReadOnlyList<RecentActivity>, AeroError>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default);
}

/// <summary>
/// Typed client for dashboard endpoints.
/// </summary>
public class DashboardHttpClient(HttpClient httpClient, ILogger<DashboardHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), IDashboardHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/dashboard";

    /// <inheritdoc />
    public Task<Result<DashboardStats, AeroError>> GetStatsAsync(CancellationToken ct = default)
    {
        return GetAsync<DashboardStats>("stats", ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<RecentActivity>, AeroError>> GetRecentActivityAsync(int count = 10, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<RecentActivity>>($"activity?count={count}", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Dashboard statistics summary.
/// </summary>
public record DashboardStats(int TotalPages, int TotalBlogs, int TotalMedia, int TotalUsers, DateTime? LastUpdated);

/// <summary>
/// Represents a single recent activity entry.
/// </summary>
public record RecentActivity(long Id, string Action, string EntityType, long EntityId, string EntityTitle, DateTime Timestamp, long UserId);
