namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface IFilesHttpClient
{
    Task<IReadOnlyList<FileSummary>> GetAllAsync(string? folder = null, CancellationToken ct = default);
    Task<FileDetail?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<FileDetail?> UploadAsync(UploadFileRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<bool> MoveAsync(long id, string destinationFolder, CancellationToken ct = default);
}

/// <summary>
/// Typed client for files endpoints (stub implementation).
/// </summary>
public class FilesHttpClient(HttpClient httpClient, ILogger<FilesHttpClient> logger) : AeroClientBase(httpClient, logger), IFilesHttpClient
{
    protected override string ResourceName => "files";

    public Task<IReadOnlyList<FileSummary>> GetAllAsync(string? folder = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrEmpty(folder) ? string.Empty : $"?folder={Uri.EscapeDataString(folder)}";
        return GetAsync<IReadOnlyList<FileSummary>>(path, ct) 
            ?? Task.FromResult<IReadOnlyList<FileSummary>>(Array.Empty<FileSummary>());
    }

    public Task<FileDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<FileDetail>($"details/{id}", ct);
    }

    public Task<FileDetail?> UploadAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        return PostAsync<FileDetail?, UploadFileRequest>(string.Empty, request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }

    public Task<bool> MoveAsync(long id, string destinationFolder, CancellationToken ct = default)
    {
        return PostAsync<bool, object>($"{id}/move?folder={Uri.EscapeDataString(destinationFolder)}", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record FileSummary(long Id, string Name, string Path, long Size, DateTime CreatedAt, DateTime ModifiedAt);
public record FileDetail(long Id, string Name, string Path, long Size, string MimeType, DateTime CreatedAt, DateTime ModifiedAt, string? Content);
public record UploadFileRequest(string Name, string Folder, long Size, string MimeType, string? Content);
