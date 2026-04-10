namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for navigations HTTP client.
/// </summary>
public interface INavigationsHttpClient
{
    /// <summary>
    /// Gets all navigation menus.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of navigation summaries or an error.</returns>
    Task<Result<IReadOnlyList<NavigationSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a navigation menu detail by its identifier.
    /// </summary>
    /// <param name="id">The navigation identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The navigation detail or an error.</returns>
    Task<Result<NavigationDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new navigation menu.
    /// </summary>
    /// <param name="request">The create navigation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created navigation detail or an error.</returns>
    Task<Result<NavigationDetail, AeroError>> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing navigation menu.
    /// </summary>
    /// <param name="id">The navigation identifier to update.</param>
    /// <param name="request">The update navigation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated navigation detail or an error.</returns>
    Task<Result<NavigationDetail, AeroError>> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a navigation menu.
    /// </summary>
    /// <param name="id">The navigation identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for navigations endpoints.
/// </summary>
public class NavigationsHttpClient(HttpClient httpClient, ILogger<NavigationsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), INavigationsHttpClient
{
    /// <inheritdoc />
    protected override string ResourceName => "navigations";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<NavigationSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<NavigationSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<NavigationDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<NavigationDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<NavigationDetail, AeroError>> CreateAsync(CreateNavigationRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateNavigationRequest, NavigationDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<NavigationDetail, AeroError>> UpdateAsync(long id, UpdateNavigationRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateNavigationRequest, NavigationDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a navigation menu.
/// </summary>
public record NavigationSummary(long Id, string Name, string? Title, int ItemCount, DateTime CreatedAt);

/// <summary>
/// Detailed navigation menu information.
/// </summary>
public record NavigationDetail(long Id, string Name, string? Title, IReadOnlyList<NavigationItemDetail> Items, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>
/// Detailed navigation item information.
/// </summary>
public record NavigationItemDetail(long Id, string Label, string? Url, long? PageId, int Order, string? AltText);

/// <summary>
/// Request to create a new navigation menu.
/// </summary>
public record CreateNavigationRequest(string Name, string? Title, IReadOnlyList<CreateNavigationItemRequest> Items);

/// <summary>
/// Request to update an existing navigation menu.
/// </summary>
public record UpdateNavigationRequest(string Name, string? Title, IReadOnlyList<UpdateNavigationItemRequest> Items);

/// <summary>
/// Request to create a navigation menu item.
/// </summary>
public record CreateNavigationItemRequest(string Label, string? Url, long? PageId, int Order, string? AltText);

/// <summary>
/// Request to update a navigation menu item.
/// </summary>
public record UpdateNavigationItemRequest(long Id, string Label, string? Url, long? PageId, int Order, string? AltText);
