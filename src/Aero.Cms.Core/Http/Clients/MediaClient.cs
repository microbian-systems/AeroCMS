namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IMediaHttpClient
{
    Task<Result<string, IReadOnlyList<MediaSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, MediaDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, MediaDetail>> UploadAsync(UploadMediaRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for media endpoints (stub implementation).
/// </summary>
public class MediaHttpClient(HttpClient httpClient, ILogger<MediaHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IMediaHttpClient
{
    protected override string ResourceName => "media";

    public Task<Result<string, IReadOnlyList<MediaSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<MediaSummary>>(string.Empty, ct);
    }

    public Task<Result<string, MediaDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<MediaDetail>($"details/{id}", ct);
    }

    public Task<Result<string, MediaDetail>> UploadAsync(UploadMediaRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<UploadMediaRequest, MediaDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record MediaSummary(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt);
public record MediaDetail(long Id, string FileName, string Url, string MimeType, long FileSize, DateTime CreatedAt, int Width, int Height, string? AltText, string? Description);
public record UploadMediaRequest(string FileName, string MimeType, long FileSize, string? AltText, string? Description);
