namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public class PagesClient : AeroClientBase
{
    protected override string ResourceName => "pages";

    public PagesClient(HttpClient httpClient, ILogger<PagesClient> logger)
        : base(httpClient, logger)
    {
    }

    public Task<IReadOnlyList<PageSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<PageSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<PageSummary>>(Array.Empty<PageSummary>());
    }

    public Task<PageDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>($"details/{id}", ct);
    }

    public Task<PageDetail?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>($"slug/{Uri.EscapeDataString(slug)}", ct);
    }

    public Task<IReadOnlyList<PageSummary>> GetPublishedAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<PageSummary>>("published", ct) 
            ?? Task.FromResult<IReadOnlyList<PageSummary>>(Array.Empty<PageSummary>());
    }

    public Task<PageDetail?> CreateAsync(CreatePageRequest request, CancellationToken ct = default)
    {
        return PostAsync<PageDetail?, CreatePageRequest>(string.Empty, request, ct);
    }

    public Task<PageDetail?> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default)
    {
        return PutAsync<PageDetail?, UpdatePageRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }

    public Task<PageDetail?> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<PageDetail?, object>($"{id}/publish", new object(), ct) 
            ?? Task.FromResult<PageDetail?>(null);
    }

    public Task<PageDetail?> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<PageDetail?, object>($"{id}/unpublish", new object(), ct) 
            ?? Task.FromResult<PageDetail?>(null);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record PageSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt);
public record PageDetail(long Id, string Title, string Slug, string Content, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, string? Excerpt, IReadOnlyList<long> BlockIds);
public record CreatePageRequest(string Title, string Slug, string Content, string? Excerpt, IReadOnlyList<long> BlockIds);
public record UpdatePageRequest(string Title, string Slug, string Content, string? Excerpt, IReadOnlyList<long> BlockIds);
