namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

/// <summary>
/// Typed client for media endpoints (stub implementation).
/// </summary>
public class MediaClient : AeroClientBase
{
    protected override string ResourceName => "media";

    public MediaClient(HttpClient httpClient, ILogger<MediaClient> logger)
        : base(httpClient, logger)
    {
    }

    public Task<IReadOnlyList<MediaSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<MediaSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<MediaSummary>>(Array.Empty<MediaSummary>());
    }

    public Task<MediaDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<MediaDetail>($"details/{id}", ct);
    }

    public Task<MediaDetail?> UploadAsync(UploadMediaRequest request, CancellationToken ct = default)
    {
        return PostAsync<MediaDetail?, UploadMediaRequest>(string.Empty, request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record MediaSummary(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt);
public record MediaDetail(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt, int Width, int Height, string? AltText, string? Description);
public record UploadMediaRequest(string FileName, string MimeType, long FileSize, string? AltText, string? Description);
