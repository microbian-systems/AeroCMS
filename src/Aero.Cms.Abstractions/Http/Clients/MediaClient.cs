namespace Aero.Cms.Core.Http.Clients;

using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for media HTTP client.
/// </summary>
public interface IMediaHttpClient
{
    /// <summary>
    /// Gets all media with pagination, search, and parent folder filtering.
    /// </summary>
    /// <param name="parentId">The optional parent folder identifier.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of media summaries or an error.</returns>
    Task<Result<PagedResult<MediaSummary>, AeroError>> GetAllAsync(long? parentId = null, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a media detail by its identifier.
    /// </summary>
    /// <param name="id">The media identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The media detail or an error.</returns>
    Task<Result<MediaDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Uploads new media.
    /// </summary>
    /// <param name="request">The upload media request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The uploaded media detail or an error.</returns>
    Task<Result<MediaDetail, AeroError>> UploadAsync(UploadMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates existing media metadata.
    /// </summary>
    /// <param name="id">The media identifier.</param>
    /// <param name="request">The update media request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated media detail or an error.</returns>
    Task<Result<MediaDetail, AeroError>> UpdateAsync(long id, UploadMediaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates a new media folder.
    /// </summary>
    /// <param name="request">The create folder request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created folder detail or an error.</returns>
    Task<Result<MediaDetail, AeroError>> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a media item or folder.
    /// </summary>
    /// <param name="id">The media identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for media endpoints.
/// </summary>
public class MediaHttpClient(HttpClient httpClient, ILogger<MediaHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IMediaHttpClient
{
    /// <inheritdoc />
    protected override string ResourceName => "media";

    /// <inheritdoc />
    public Task<Result<PagedResult<MediaSummary>, AeroError>> GetAllAsync(long? parentId = null, int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (parentId.HasValue) url += $"&parentId={parentId}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<MediaSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<MediaDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<MediaDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<MediaDetail, AeroError>> UploadAsync(UploadMediaRequest request, CancellationToken ct = default)
    {
        return PostAsync<UploadMediaRequest, MediaDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<MediaDetail, AeroError>> UpdateAsync(long id, UploadMediaRequest request, CancellationToken ct = default)
    {
        return PutAsync<UploadMediaRequest, MediaDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<MediaDetail, AeroError>> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateFolderRequest, MediaDetail>("folder", request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for media items or folders.
/// </summary>
public record MediaSummary(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt, bool IsFolder = false, long? ParentId = null);

/// <summary>
/// Detailed information for media including dimensions for images.
/// </summary>
public record MediaDetail(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt, int Width, int Height, string? AltText, string? Description, bool IsFolder = false, long? ParentId = null);

/// <summary>
/// Request to upload or update media metadata.
/// </summary>
public record UploadMediaRequest(string FileName, string MimeType, long FileSize, string? AltText = null, string? Description = null, long? ParentId = null, string? Base64Data = null);

/// <summary>
/// Request to create a new folder.
/// </summary>
public record CreateFolderRequest(string Name, long? ParentId = null);
