namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;
using Aero.Core.Railway;

public interface IUsersHttpClient
{
    Task<Result<string, IReadOnlyList<UserSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, UserDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, UserDetail>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<string, UserDetail>> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<string, bool>> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default);
}

/// <summary>
/// Typed client for users endpoints.
/// </summary>
public class UsersHttpClient(HttpClient httpClient, ILogger<UsersHttpClient> logger) 
    : AeroCmsClientBase(httpClient, logger), IUsersHttpClient
{
    protected override string ResourceName => "users";

    public Task<Result<string, IReadOnlyList<UserSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<UserSummary>>(string.Empty, ct);
    }

    public Task<Result<string, UserDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<UserDetail>($"details/{id}", ct);
    }

    public Task<Result<string, UserDetail>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreateUserRequest, UserDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, UserDetail>> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateUserRequest, UserDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return base.DeleteResultAsync(id.ToString(), ct);
    }

    public Task<Result<string, bool>> ChangePasswordAsync(long id, ChangePasswordRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<ChangePasswordRequest, bool>($"{id}/password", request, ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record UserSummary(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt);
public record UserDetail(long Id, string UserName, string Email, string DisplayName, bool IsEnabled, DateTime CreatedAt, DateTime? LastLoginAt, IReadOnlyList<string> Roles);
public record CreateUserRequest(string UserName, string Email, string DisplayName, string Password, IReadOnlyList<string> Roles);
public record UpdateUserRequest(string Email, string DisplayName, bool IsEnabled, IReadOnlyList<string> Roles);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
