namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface INavigationsHttpClient
{
    Task<IReadOnlyList<NavigationSummary>> GetAllAsync(CancellationToken ct = default);
    Task<NavigationDetail?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<NavigationDetail?> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default);
    Task<NavigationDetail?> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for navigations endpoints (stub implementation).
/// </summary>
public class NavigationsHttpClient(HttpClient httpClient, ILogger<NavigationsHttpClient> logger)
    : AeroClientBase(httpClient, logger), INavigationsHttpClient
{
    protected override string ResourceName => "navigations";

    public Task<IReadOnlyList<NavigationSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<NavigationSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<NavigationSummary>>(Array.Empty<NavigationSummary>());
    }

    public Task<NavigationDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<NavigationDetail>($"details/{id}", ct);
    }

    public Task<NavigationDetail?> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default)
    {
        return PostAsync<NavigationDetail?, CreateNavigationRequest>(string.Empty, request, ct);
    }

    public Task<NavigationDetail?> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default)
    {
        return PutAsync<NavigationDetail?, UpdateNavigationRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record NavigationSummary(long Id, string Name, string Location, int ItemCount, DateTime CreatedAt);
public record NavigationDetail(long Id, string Name, string Location, IReadOnlyList<NavigationItemDetail> Items, DateTime CreatedAt, DateTime UpdatedAt);
public record NavigationItemDetail(long Id, string Label, string? Url, long? PageId, int Order, long? ParentId);
public record CreateNavigationRequest(string Name, string Location, IReadOnlyList<CreateNavigationItemRequest> Items);
public record UpdateNavigationRequest(string Name, string Location, IReadOnlyList<UpdateNavigationItemRequest> Items);
public record CreateNavigationItemRequest(string Label, string? Url, long? PageId, int Order, long? ParentId);
public record UpdateNavigationItemRequest(long Id, string Label, string? Url, long? PageId, int Order, long? ParentId);
