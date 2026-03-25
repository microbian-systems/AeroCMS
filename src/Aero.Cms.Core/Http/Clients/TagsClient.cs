namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface ITagsHttpClient
{
    Task<Result<string, IReadOnlyList<TagSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, TagDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, TagDetail>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<Result<string, TagDetail>> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for tags endpoints (stub implementation).
/// </summary>
public class TagsHttpClient(HttpClient httpClient, ILogger<TagsHttpClient> logger)
    : AeroCmsClientBase(httpClient, logger), ITagsHttpClient
{
    protected override string ResourceName => "tags";

    public Task<Result<string, IReadOnlyList<TagSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<TagSummary>>(string.Empty, ct);
    }

    public Task<Result<string, TagDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<TagDetail>($"details/{id}", ct);
    }

    public Task<Result<string, TagDetail>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreateTagRequest, TagDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, TagDetail>> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateTagRequest, TagDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record TagSummary(long Id, string Name, string Slug, int ContentCount);
public record TagDetail(long Id, string Name, string Slug, string? Description, int ContentCount, DateTime CreatedAt);
public record CreateTagRequest(string Name, string Slug, string? Description);
public record UpdateTagRequest(string Name, string Slug, string? Description);
