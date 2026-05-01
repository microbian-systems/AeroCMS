namespace Aero.Cms.Abstractions.Http.Clients;

using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Enums;
using Aero.Core.Entities;
using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for blog HTTP client.
/// </summary>
public interface IBlogHttpClient
{
    /// <summary>
    /// Gets all blog posts with pagination and optional search.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of blog summaries or an error.</returns>
    Task<Result<PagedResult<BlogSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a blog post by its identifier.
    /// </summary>
    /// <param name="id">The blog post identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new blog post.
    /// </summary>
    /// <param name="request">The create blog request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to update.</param>
    /// <param name="request">The update blog request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Publishes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a blog post.
    /// </summary>
    /// <param name="id">The blog post identifier to unpublish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated blog post detail or an error.</returns>
    Task<Result<BlogDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Typed client for blog endpoints.
/// </summary>
public class BlogHttpClient(HttpClient httpClient, ILogger<BlogHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IBlogHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/blogs";

    /// <inheritdoc />
    public Task<Result<PagedResult<BlogSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<BlogSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<BlogDetail>($"{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> CreateAsync(CreateBlogRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreateBlogRequest, BlogDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> UpdateAsync(long id, UpdateBlogRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdateBlogRequest, BlogDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var response = await base.DeleteAsync(id.ToString(), ct);
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from DeleteAsync")
        };
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, BlogDetail>($"{id}/publish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<BlogDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PostAsync<object, BlogDetail>($"{id}/unpublish", new object(), ct);
    }
}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a blog post.
/// </summary>
/// <param name="Id">The blog post identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="PublishedAt">The publication time.</param>
/// <param name="Excerpt">The post excerpt.</param>
/// <param name="FeaturedImageUrl">The featured image URL.</param>
public record BlogSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl);

/// <summary>
/// Detailed information for a blog post.
/// </summary>
public record BlogDetail(
    long Id,
    string Title,
    string Slug,
    string? Excerpt,
    string? SeoTitle,
    string? SeoDescription,
    DateTimeOffset? PublishedOn,
    int PublicationState,
    List<BlockBase>? Content,
    List<long> TagIds,
    List<long> CategoryIds,
    long? AuthorId,
    string? ImageUrl,
    int Likes,
    DateTimeOffset CreatedOn,
    DateTimeOffset? ModifiedOn);

/// <summary>
/// Request to create a new blog post.
/// </summary>
public class CreateBlogRequest
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? MarkdownContent { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public int PublicationState { get; set; }
}

/// <summary>
/// Request to update an existing blog post.
/// </summary>
public class UpdateBlogRequest
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? MarkdownContent { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public string? ImageUrl { get; set; }
    public int PublicationState { get; set; }
}
