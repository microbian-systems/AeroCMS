namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for files HTTP client.
/// </summary>
public interface IFilesHttpClient
{
    /// <summary>
    /// Gets all files with optional folder filtering.
    /// </summary>
    /// <param name="folder">The folder path to filter files by.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of file summaries or an error.</returns>
    Task<Result<IReadOnlyList<FileSummary>, AeroError>> GetAllAsync(string? folder = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a file detail by its identifier.
    /// </summary>
    /// <param name="id">The file identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The file detail or an error.</returns>
    Task<Result<FileDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Uploads a new file.
    /// </summary>
    /// <param name="request">The file upload request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The uploaded file detail or an error.</returns>
    Task<Result<FileDetail, AeroError>> UploadAsync(UploadFileRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="id">The file identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Moves a file to a new destination folder.
    /// </summary>
    /// <param name="id">The file identifier.</param>
    /// <param name="destinationFolder">The destination folder path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if movement was successful or an error.</returns>
    Task<Result<bool, AeroError>> MoveAsync(long id, string destinationFolder, CancellationToken ct = default);
}

/// <summary>
/// Typed client for files endpoints.
/// </summary>
public class FilesHttpClient(HttpClient httpClient, ILogger<FilesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IFilesHttpClient
{
    /// <inheritdoc />
    public override string Path => "files";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<FileSummary>, AeroError>> GetAllAsync(string? folder = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrEmpty(folder) ? string.Empty : $"?folder={Uri.EscapeDataString(folder)}";
        return GetAsync<IReadOnlyList<FileSummary>>(path, ct);
    }

    /// <inheritdoc />
    public Task<Result<FileDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<FileDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<FileDetail, AeroError>> UploadAsync(UploadFileRequest request, CancellationToken ct = default)
    {
        return PostAsync<UploadFileRequest, FileDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> MoveAsync(long id, string destinationFolder, CancellationToken ct = default)
    {
        return MapBoolResult(base.PostAsync<object, HttpResponseMessage>($"{id}/move?folder={Uri.EscapeDataString(destinationFolder)}", new object(), ct));
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a file.
/// </summary>
public record FileSummary(long Id, string Name, string Path, long Size, DateTime CreatedAt, DateTime ModifiedAt);

/// <summary>
/// Detailed information for a file including its content if available.
/// </summary>
public record FileDetail(long Id, string Name, string Path, long Size, string MimeType, DateTime CreatedAt, DateTime ModifiedAt, string? Content);

/// <summary>
/// Request to upload a new file.
/// </summary>
public record UploadFileRequest(string Name, string Folder, long Size, string MimeType, string? Content);
