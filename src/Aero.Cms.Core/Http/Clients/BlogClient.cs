namespace Aero.Cms.Core.Http.Clients;

using Microsoft.Extensions.Logging;

public interface IBlogHttpClient
{
    Task<IReadOnlyList<BlogSummary>> GetAllAsync(CancellationToken ct = default);
    Task<BlogDetail?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<BlogSummary>> GetPublishedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BlogSummary>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<BlogSummary>> SearchAsync(string query, CancellationToken ct = default);
    Task<BlogDetail?> CreateAsync(CreateBlogRequest request, CancellationToken ct = default);
    Task<BlogDetail?> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
    Task<BlogDetail?> PublishAsync(long id, CancellationToken ct = default);
    Task<BlogDetail?> UnpublishAsync(long id, CancellationToken ct = default);
}

public class BlogHttpClient(HttpClient httpClient, ILogger<BlogHttpClient> logger) : AeroClientBase(httpClient, logger), IBlogHttpClient
{
    protected override string ResourceName => "blogs";

    public Task<IReadOnlyList<BlogSummary>> GetAllAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogSummary>>(string.Empty, ct) 
            ?? Task.FromResult<IReadOnlyList<BlogSummary>>(Array.Empty<BlogSummary>());
    }

    public Task<BlogDetail?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<BlogDetail>($"details/{id}", ct);
    }

    public Task<IReadOnlyList<BlogSummary>> GetPublishedAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogSummary>>("published", ct) 
            ?? Task.FromResult<IReadOnlyList<BlogSummary>>(Array.Empty<BlogSummary>());
    }

    public Task<IReadOnlyList<BlogSummary>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogSummary>>($"category/{categoryId}", ct) 
            ?? Task.FromResult<IReadOnlyList<BlogSummary>>(Array.Empty<BlogSummary>());
    }

    public Task<IReadOnlyList<BlogSummary>> SearchAsync(string query, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogSummary>>($"search?q={Uri.EscapeDataString(query)}", ct) 
            ?? Task.FromResult<IReadOnlyList<BlogSummary>>(Array.Empty<BlogSummary>());
    }

    public Task<BlogDetail?> CreateAsync(CreateBlogRequest request, CancellationToken ct = default)
    {
        return PostAsync<BlogDetail?, CreateBlogRequest>(string.Empty, request, ct);
    }

    public Task<BlogDetail?> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default)
    {
        return PutAsync<BlogDetail?, UpdateBlogRequest>(id.ToString(), request, ct);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteAsync(id.ToString(), ct);
    }

    public Task<BlogDetail?> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<BlogDetail?, object>($"{id}/publish", new object(), ct) 
            ?? Task.FromResult<BlogDetail?>(null);
    }

    public Task<BlogDetail?> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<BlogDetail?, object>($"{id}/unpublish", new object(), ct) 
            ?? Task.FromResult<BlogDetail?>(null);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record BlogSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl);
public record BlogDetail(long Id, string Title, string Slug, string Content, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);
public record CreateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);
public record UpdateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long CategoryId, IReadOnlyList<long> TagIds);
