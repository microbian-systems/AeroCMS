namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IPagesHttpClient
{
    Task<Result<string, PagedResult<PageSummary>>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);
    Task<Result<string, PageDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, PageDetail>> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Result<string, PagedResult<PageSummary>>> GetPublishedAsync(int skip = 0, int take = 10, CancellationToken ct = default);
    Task<Result<string, PageDetail>> CreateAsync(CreatePageRequest request, CancellationToken ct = default);
    Task<Result<string, PageDetail>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<string, PageDetail>> PublishAsync(long id, CancellationToken ct = default);
    Task<Result<string, PageDetail>> UnpublishAsync(long id, CancellationToken ct = default);
}

public class PagesHttpClient(HttpClient httpClient, ILogger<PagesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IPagesHttpClient
{
    protected override string ResourceName => "pages";

    public Task<Result<string, PagedResult<PageSummary>>> GetAllAsync(int skip = 0, int take = 20, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetResultAsync<PagedResult<PageSummary>>(url, ct);
    }

    public Task<Result<string, PageDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<PageDetail>($"details/{id}", ct);
    }

    public Task<Result<string, PageDetail>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return GetResultAsync<PageDetail>($"slug/{Uri.EscapeDataString(slug)}", ct);
    }

    public Task<Result<string, PagedResult<PageSummary>>> GetPublishedAsync(int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return GetResultAsync<PagedResult<PageSummary>>($"published?skip={skip}&take={take}", ct);
    }

    public Task<Result<string, PageDetail>> CreateAsync(CreatePageRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreatePageRequest, PageDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, PageDetail>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdatePageRequest, PageDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }

    public Task<Result<string, PageDetail>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PageDetail>($"{id}/publish", new object(), ct);
    }

    public Task<Result<string, PageDetail>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, PageDetail>($"{id}/unpublish", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record PageSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt);
public record PageDetail(
    long Id, 
    string Title, 
    string Slug, 
    string? Excerpt, 
    string? SeoTitle, 
    string? SeoDescription,
    DateTime CreatedAt, 
    DateTime UpdatedAt, 
    DateTime? PublishedAt, 
    ContentPublicationState PublicationState,
    int BlockCount);

public record CreatePageRequest(string Title, string Slug, string Content, string? Excerpt, IReadOnlyList<long> BlockIds)
{
    public string Summary { get; set; } = string.Empty;
    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public ContentPublicationState PublicationState { get; set; }
}

public record UpdatePageRequest(
    string Title,
    string Slug,
    string Content,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    string? Excerpt,
    IReadOnlyList<long> BlockIds)
{
    public ContentPublicationState PublicationState { get; set; }
}
