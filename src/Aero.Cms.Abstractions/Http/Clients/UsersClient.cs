namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for users HTTP client.
/// </summary>
public interface IUsersHttpClient
{
    /// <summary>
    /// Gets all users with pagination and optional search.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of user summaries or an error.</returns>
    Task<Result<PagedResult<UserSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific user.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The user detail or an error.</returns>
    Task<Result<UserDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request">The create user request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created user detail or an error.</returns>
    Task<Result<UserDetail, AeroError>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="id">The user identifier to update.</param>
    /// <param name="request">The update user request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated user detail or an error.</returns>
    Task<Result<UserDetail, AeroError>> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a user by ID.
    /// </summary>
    /// <param name="id">The user identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="request">The password change request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if password was changed successfully or an error.</returns>
    Task<Result<bool, AeroError>> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default);
}

/// <summary>
/// Typed client for users endpoints.
/// </summary>
public class UsersHttpClient(HttpClient httpClient, ILogger<UsersHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IUsersHttpClient
{
    /// <inheritdoc />
    protected override string ResourceName => "users";

    /// <inheritdoc />
    public Task<Result<PagedResult<UserSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<UserSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<UserDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<UserDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<UserDetail, AeroError>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateUserRequest, UserDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<UserDetail, AeroError>> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateUserRequest, UserDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return base.DeleteAsync(id.ToString(), ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default)
    {
        return PostAsync<ChangePasswordRequest, bool>($"{id}/password", request, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a user.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="UserName">The username.</param>
/// <param name="Email">The email address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="IsEnabled">Whether the account is enabled.</param>
/// <param name="CreatedAt">The creation time.</param>
public record UserSummary(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt);

/// <summary>
/// Detailed information for a user.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="UserName">The username.</param>
/// <param name="Email">The email address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="IsEnabled">Whether the account is enabled.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="LastLoginAt">The last login time.</param>
/// <param name="Roles">The list of assigned roles.</param>
public record UserDetail(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt, DateTime? LastLoginAt, IReadOnlyList<string> Roles);

/// <summary>
/// Request to create a new user.
/// </summary>
/// <param name="UserName">The username.</param>
/// <param name="Email">The email address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Password">The plain-text password.</param>
/// <param name="Roles">The list of assigned roles.</param>
public record CreateUserRequest(string UserName, string Email, string DisplayName, string Password, IReadOnlyList<string> Roles);

/// <summary>
/// Request to update an existing user.
/// </summary>
/// <param name="Email">The email address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="IsEnabled">Whether the account is enabled.</param>
/// <param name="Roles">The list of assigned roles.</param>
public record UpdateUserRequest(string Email, string DisplayName, bool IsEnabled, IReadOnlyList<string> Roles);

/// <summary>
/// Request to change a user's password.
/// </summary>
/// <param name="CurrentPassword">The current password.</param>
/// <param name="NewPassword">The new password.</param>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
