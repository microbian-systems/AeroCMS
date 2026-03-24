namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface IProfileHttpClient
{
    Task<UserProfile?> GetCurrentAsync(CancellationToken ct = default);
    Task<UserProfile?> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default);
    Task<UserProfile?> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default);
    Task<bool> DeleteAvatarAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed client for profile endpoints (stub implementation).
/// </summary>
public class ProfileHttpClient(HttpClient httpClient, ILogger<ProfileHttpClient> logger) : AeroClientBase(httpClient, logger), IProfileHttpClient
{
    protected override string ResourceName => "profile";

    public Task<UserProfile?> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetAsync<UserProfile>("", ct);
    }

    public Task<UserProfile?> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        return PutAsync<UserProfile?, UpdateProfileRequest>(string.Empty, request, ct);
    }

    public Task<bool> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default)
    {
        return PostAsync<bool, ChangeProfilePasswordRequest>("password", request, ct);
    }

    public Task<UserProfile?> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default)
    {
        return PostAsync<UserProfile?, UploadAvatarRequest>("avatar", request, ct);
    }

    public Task<bool> DeleteAvatarAsync(CancellationToken ct = default)
    {
        return DeleteAsync("avatar", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record UserProfile(long Id, string UserName, string Email, string DisplayName, string? AvatarUrl, IReadOnlyList<string> Roles);
public record UpdateProfileRequest(string DisplayName, string Email);
public record ChangeProfilePasswordRequest(string CurrentPassword, string NewPassword);
public record UploadAvatarRequest(string FileName, string MimeType, long FileSize, string Content);
