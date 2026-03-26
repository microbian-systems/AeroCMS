namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IProfileHttpClient
{
    Task<Result<string, UserProfile>> GetCurrentAsync(CancellationToken ct = default);
    Task<Result<string, UserProfile>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default);
    Task<Result<string, UserProfile>> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAvatarAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed client for profile endpoints (stub implementation).
/// </summary>
public class ProfileHttpClient(HttpClient httpClient, ILogger<ProfileHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IProfileHttpClient
{
    protected override string ResourceName => "profile";

    public Task<Result<string, UserProfile>> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetResultAsync<UserProfile>(string.Empty, ct);
    }

    public Task<Result<string, UserProfile>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateProfileRequest, UserProfile>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<ChangeProfilePasswordRequest, bool>("password", request, ct);
    }

    public Task<Result<string, UserProfile>> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<UploadAvatarRequest, UserProfile>("avatar", request, ct);
    }

    public Task<Result<string, bool>> DeleteAvatarAsync(CancellationToken ct = default)
    {
        return DeleteResultAsync("avatar", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record UserProfile(long Id, string UserName, string Email, string DisplayName, string? AvatarUrl, IReadOnlyList<string> Roles);
public record UpdateProfileRequest(string DisplayName, string Email);
public record ChangeProfilePasswordRequest(string CurrentPassword, string NewPassword);
public record UploadAvatarRequest(string FileName, string MimeType, long FileSize, string Content);
