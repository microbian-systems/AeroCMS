using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Headless.Api.v1;

/// <summary>
/// Admin API for dashboard statistics and overview.
/// </summary>
public static class DashboardApi
{
    /// <summary>
    /// Maps the Dashboard Admin API endpoints.
    /// </summary>
    public static void MapDashboardApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/dashboard", GetDashboardStats)
            .WithName("GetDashboardStats")
            .WithTags("Admin - Dashboard");

        app.MapGet("/api/v1/admin/dashboard/recent-activity", GetRecentActivity)
            .WithName("GetRecentActivity")
            .WithTags("Admin - Dashboard");

        app.MapGet("/api/v1/admin/dashboard/content-summary", GetContentSummary)
            .WithName("GetContentSummary")
            .WithTags("Admin - Dashboard");
    }

    private static async Task<IResult> GetDashboardStats(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("DashboardApi.GetDashboardStats is not yet implemented");
    }

    private static async Task<IResult> GetRecentActivity(int count = 10, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("DashboardApi.GetRecentActivity is not yet implemented");
    }

    private static async Task<IResult> GetContentSummary(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        throw new NotImplementedException("DashboardApi.GetContentSummary is not yet implemented");
    }
}
