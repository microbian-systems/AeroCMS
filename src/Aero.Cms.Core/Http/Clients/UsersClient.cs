namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface IUsersHttpClient
{
    Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default);
    Task<UserDetail?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserDetail?> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDetail?> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default);
}

/// <summary>
/// Typed client for users endpoints (stub implementation).
/// </summary>
public class UsersHttpClient(HttpClient httpClient, ILogger<UsersHttpClient> logger) : AeroClientBase(httpClient, logger), IUsersHttpClient
{
    protected override string ResourceName => "users";

    public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<UserSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<UserSummary>>(Array.Empty<UserSummary>());
    }

    public Task<UserDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<UserDetail>($"details/{id}", ct);
    }

    public Task<UserDetail?> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        return PostAsync<UserDetail?, CreateUserRequest>(string.Empty, request, ct);
    }

    public Task<UserDetail?> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default)
    {
        return PutAsync<UserDetail?, UpdateUserRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }

    public Task<bool> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default)
    {
        return PostAsync<bool, ChangePasswordRequest>($"{id}/password", request, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record UserSummary(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt);
public record UserDetail(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt, DateTime? LastLoginAt, IReadOnlyList<string> Roles);
public record CreateUserRequest(string UserName, string Email, string DisplayName, string Password, IReadOnlyList<string> Roles);
public record UpdateUserRequest(string Email, string DisplayName, bool IsEnabled, IReadOnlyList<string> Roles);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
