namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for profile HTTP client.
/// </summary>
public interface IProfileHttpClient
{
    /// <summary>
    /// Gets the current user's profile.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The user profile or an error.</returns>
    Task<Result<UserProfile, AeroError>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the current user's profile.
    /// </summary>
    /// <param name="request">The update profile request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated user profile or an error.</returns>
    Task<Result<UserProfile, AeroError>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    /// <param name="request">The password change request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if password was changed successfully or an error.</returns>
    Task<Result<bool, AeroError>> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Uploads an avatar for the current user.
    /// </summary>
    /// <param name="request">The avatar upload request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated user profile or an error.</returns>
    Task<Result<UserProfile, AeroError>> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the current user's avatar.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAvatarAsync(CancellationToken ct = default);
}

/// <summary>
/// Typed client for profile endpoints.
/// </summary>
public class ProfileHttpClient(HttpClient httpClient, ILogger<ProfileHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IProfileHttpClient
{
    /// <inheritdoc />
    protected override string ResourceName => "profile";

    /// <inheritdoc />
    public Task<Result<UserProfile, AeroError>> GetCurrentAsync(CancellationToken ct = default)
    {
        return GetAsync<UserProfile>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<UserProfile, AeroError>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateProfileRequest, UserProfile>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> ChangePasswordAsync(ChangeProfilePasswordRequest request, CancellationToken ct = default)
    {
        return PostAsync<ChangeProfilePasswordRequest, bool>("password", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<UserProfile, AeroError>> UploadAvatarAsync(UploadAvatarRequest request, CancellationToken ct = default)
    {
        return PostAsync<UploadAvatarRequest, UserProfile>("avatar", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAvatarAsync(CancellationToken ct = default)
    {
        return base.DeleteAsync("avatar", ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Information about a user profile.
/// </summary>
/// <param name="Id">The user identifier.</param>
/// <param name="UserName">The username.</param>
/// <param name="Email">The email address.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="AvatarUrl">The optional avatar URL.</param>
/// <param name="Roles">The list of assigned roles.</param>
public record UserProfile(long Id, string UserName, string Email, string DisplayName, string? AvatarUrl, IReadOnlyList<string> Roles);

/// <summary>
/// Request to update a user profile.
/// </summary>
/// <param name="DisplayName">The new display name.</param>
/// <param name="Email">The new email address.</param>
public record UpdateProfileRequest(string DisplayName, string Email);

/// <summary>
/// Request to change a profile password.
/// </summary>
/// <param name="CurrentPassword">The current password.</param>
/// <param name="NewPassword">The new password.</param>
public record ChangeProfilePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Request to upload a user avatar.
/// </summary>
/// <param name="FileName">The filename.</param>
/// <param name="MimeType">The MIME type.</param>
/// <param name="FileSize">The file size in bytes.</param>
/// <param name="Content">The base64 encoded image content.</param>
public record UploadAvatarRequest(string FileName, string MimeType, long FileSize, string Content);
