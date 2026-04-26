namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for tags HTTP client.
/// </summary>
public interface ITagsHttpClient
{
    /// <summary>
    /// Gets all tags.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of tag summaries or an error.</returns>
    Task<Result<IReadOnlyList<TagSummary>, AeroError>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets tag detail by ID.
    /// </summary>
    /// <param name="id">The tag identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The tag detail or an error.</returns>
    Task<Result<TagDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new tag.
    /// </summary>
    /// <param name="request">The create tag request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created tag detail or an error.</returns>
    Task<Result<TagDetail, AeroError>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing tag.
    /// </summary>
    /// <param name="id">The tag identifier to update.</param>
    /// <param name="request">The update tag request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated tag detail or an error.</returns>
    Task<Result<TagDetail, AeroError>> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a tag by ID.
    /// </summary>
    /// <param name="id">The tag identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for tags endpoints.
/// </summary>
public class TagsHttpClient(HttpClient httpClient, ILogger<TagsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ITagsHttpClient
{
    /// <inheritdoc />
    public override string Path => "tags";

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<TagSummary>, AeroError>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<TagSummary>>(string.Empty, ct);
    }

    /// <inheritdoc />
    public Task<Result<TagDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<TagDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<TagDetail, AeroError>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateTagRequest, TagDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<TagDetail, AeroError>> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateTagRequest, TagDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
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
/// Summary information for a tag.
/// </summary>
/// <param name="Id">The tag identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="ContentCount">The number of items tagged.</param>
public record TagSummary(long Id, string Name, string Slug, int ContentCount);

/// <summary>
/// Detailed information for a tag.
/// </summary>
/// <param name="Id">The tag identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
/// <param name="ContentCount">The number of items tagged.</param>
/// <param name="CreatedAt">The creation time.</param>
public record TagDetail(long Id, string Name, string Slug, string? Description, int ContentCount, DateTime CreatedAt);

/// <summary>
/// Request to create a new tag.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
public record CreateTagRequest(string Name, string Slug, string? Description);

/// <summary>
/// Request to update an existing tag.
/// </summary>
/// <param name="Name">The display name.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Description">The optional description.</param>
public record UpdateTagRequest(string Name, string Slug, string? Description);
