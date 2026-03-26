namespace Aero.Cms.Core.Http.Clients;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

public interface IBlogHttpClient
{
    Task<Result<string, IReadOnlyList<BlogSummary>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<string, BlogDetail>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<BlogSummary>>> GetPublishedAsync(CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<BlogSummary>>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<Result<string, IReadOnlyList<BlogSummary>>> SearchAsync(string query, CancellationToken ct = default);
    Task<Result<string, BlogDetail>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default);
    Task<Result<string, BlogDetail>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default);
    Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default);
    Task<Result<string, BlogDetail>> PublishAsync(long id, CancellationToken ct = default);
    Task<Result<string, BlogDetail>> UnpublishAsync(long id, CancellationToken ct = default);
}

public class BlogHttpClient(HttpClient httpClient, ILogger<BlogHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IBlogHttpClient
{
    protected override string ResourceName => "blogs";

    public Task<Result<string, IReadOnlyList<BlogSummary>>> GetAllAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<BlogSummary>>(string.Empty, ct);
    }

    public Task<Result<string, BlogDetail>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetResultAsync<BlogDetail>($"details/{id}", ct);
    }

    public Task<Result<string, IReadOnlyList<BlogSummary>>> GetPublishedAsync(CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<BlogSummary>>("published", ct);
    }

    public Task<Result<string, IReadOnlyList<BlogSummary>>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<BlogSummary>>($"category/{categoryId}", ct);
    }

    public Task<Result<string, IReadOnlyList<BlogSummary>>> SearchAsync(string query, CancellationToken ct = default)
    {
        return GetResultAsync<IReadOnlyList<BlogSummary>>($"search?q={Uri.EscapeDataString(query)}", ct);
    }

    public Task<Result<string, BlogDetail>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default)
    {
        return PostResultAsync<CreateBlogRequest, BlogDetail>(string.Empty, request, ct);
    }

    public Task<Result<string, BlogDetail>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default)
    {
        return PutResultAsync<UpdateBlogRequest, BlogDetail>(id.ToString(), request, ct);
    }

    public Task<Result<string, bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return DeleteResultAsync(id.ToString(), ct);
    }

    public Task<Result<string, BlogDetail>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, BlogDetail>($"{id}/publish", new object(), ct);
    }

    public Task<Result<string, BlogDetail>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostResultAsync<object, BlogDetail>($"{id}/unpublish", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

public record BlogSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl);
public record BlogDetail(long Id, string Title, string Slug, string Content, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);
public record CreateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);
public record UpdateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long CategoryId, IReadOnlyList<long> TagIds);
