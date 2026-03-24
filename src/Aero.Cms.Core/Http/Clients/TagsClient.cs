namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface ITagsHttpClient
{
    Task<IReadOnlyList<TagSummary>> GetAllAsync(CancellationToken ct = default);
    Task<TagDetail?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<TagDetail?> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<TagDetail?> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for tags endpoints (stub implementation).
/// </summary>
public class TagsHttpClient(HttpClient httpClient, ILogger<TagsHttpClient> logger) : AeroClientBase(httpClient, logger), ITagsHttpClient
{
    protected override string ResourceName => "tags";

    public Task<IReadOnlyList<TagSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<TagSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<TagSummary>>(Array.Empty<TagSummary>());
    }

    public Task<TagDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<TagDetail>($"details/{id}", ct);
    }

    public Task<TagDetail?> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        return PostAsync<TagDetail?, CreateTagRequest>(string.Empty, request, ct);
    }

    public Task<TagDetail?> UpdateAsync(long id, UpdateTagRequest request, CancellationToken ct = default)
    {
        return PutAsync<TagDetail?, UpdateTagRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record TagSummary(long Id, string Name, string Slug, int ContentCount);
public record TagDetail(long Id, string Name, string Slug, string? Description, int ContentCount, DateTime CreatedAt);
public record CreateTagRequest(string Name, string Slug, string? Description);
public record UpdateTagRequest(string Name, string Slug, string? Description);
