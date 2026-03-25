namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IFilesHttpClient
{
    Task<Result<string, IReadOnlyList<FileSummary>>> GetAllAsync(string? folder = null, CancellationToken ct = default);
    Task<Result<string, FileDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, FileDetail>> UploadAsync(UploadFileRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<string, bool>> MoveAsync(long id, string destinationFolder, CancellationToken ct = default);
}

/// <summary>
/// Typed client for files endpoints (stub implementation).
/// </summary>
public class FilesHttpClient(HttpClient httpClient, ILogger<FilesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IFilesHttpClient
{
    protected override string ResourceName => "files";

    public Task<Result<string, IReadOnlyList<FileSummary>>> GetAllAsync(string? folder = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrEmpty(folder) ? string.Empty : $"?folder={Uri.EscapeDataString(folder)}";
        return GetResultAsync<IReadOnlyList<FileSummary>>(path, ct);
    }

    public Task<Result<string, FileDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<FileDetail>($"details/{id}", ct);
    }

    public Task<Result<string, FileDetail>> UploadAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<UploadFileRequest, FileDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }

    public Task<Result<string, bool>> MoveAsync(long id, string destinationFolder, CancellationToken ct = default)
    {
        return PostResultAsync<object, bool>($"{id}/move?folder={Uri.EscapeDataString(destinationFolder)}", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record FileSummary(long Id, string Name, string Path, long Size, DateTime CreatedAt, DateTime ModifiedAt);
public record FileDetail(long Id, string Name, string Path, long Size, string MimeType, DateTime CreatedAt, DateTime ModifiedAt, string? Content);
public record UploadFileRequest(string Name, string Folder, long Size, string MimeType, string? Content);
