namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface INavigationsHttpClient
{
    Task<Result<string, IReadOnlyList<NavigationSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, NavigationDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, NavigationDetail>> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default);
    Task<Result<string, NavigationDetail>> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for navigations endpoints (stub implementation).
/// </summary>
public class NavigationsHttpClient(HttpClient httpClient, ILogger<NavigationsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), INavigationsHttpClient
{
    protected override string ResourceName => "navigations";

    public Task<Result<string, IReadOnlyList<NavigationSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<NavigationSummary>>(string.Empty, ct);
    }

    public Task<Result<string, NavigationDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<NavigationDetail>($"details/{id}", ct);
    }

    public Task<Result<string, NavigationDetail>> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreateNavigationRequest, NavigationDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, NavigationDetail>> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateNavigationRequest, NavigationDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record NavigationSummary(long Id, string Name, string? Title, int ItemCount, DateTime CreatedAt);
public record NavigationDetail(long Id, string Name, string? Title, IReadOnlyList<NavigationItemDetail> Items, DateTime CreatedAt, DateTime UpdatedAt);
public record NavigationItemDetail(long Id, string Label, string? Url, long? PageId, int Order, string? AltText);
public record CreateNavigationRequest(string Name, string? Title, IReadOnlyList<CreateNavigationItemRequest> Items);
public record UpdateNavigationRequest(string Name, string? Title, IReadOnlyList<UpdateNavigationItemRequest> Items);
public record CreateNavigationItemRequest(string Label, string? Url, long? PageId, int Order, string? AltText);
public record UpdateNavigationItemRequest(long Id, string Label, string? Url, long? PageId, int Order, string? AltText);
