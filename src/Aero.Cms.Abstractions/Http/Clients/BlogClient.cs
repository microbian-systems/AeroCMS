namespace Aero.Cms.Abstractions.Http.Clients;

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
    /// Gets published blog posts with pagination.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of blog summaries or an error.</returns>
    Task<Result<PagedResult<BlogSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 10, CancellationToken ct = default);

    /// <summary>
    /// Gets blog posts in a specific category.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of blog summaries or an error.</returns>
    Task<Result<IReadOnlyList<BlogSummary>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);

    /// <summary>
    /// Searches for blog posts.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of blog summaries or an error.</returns>
    Task<Result<PagedResult<BlogSummary>, AeroError>> SearchAsync(string query, int skip = 0, int take = 10, CancellationToken ct = default);

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
    public override string Path => "blogs";

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
        return GetAsync<BlogDetail>($"details/{id}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<BlogSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 10, CancellationToken ct = default)
    {
        return GetAsync<PagedResult<BlogSummary>>($"published?skip={skip}&take={take}", ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<BlogSummary>, AeroError>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<BlogSummary>>($"category/{categoryId}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<BlogSummary>, AeroError>> SearchAsync(string query, int skip = 0, int take = 10, CancellationToken ct = default)
    {
        return GetAsync<PagedResult<BlogSummary>>($"search?q={Uri.EscapeDataString(query)}&skip={skip}&take={take}", ct);
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
/// <param name="Id">The blog post identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Content">The post content.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="UpdatedAt">The last update time.</param>
/// <param name="PublishedAt">The publication time.</param>
/// <param name="Excerpt">The post excerpt.</param>
/// <param name="FeaturedImageUrl">The featured image URL.</param>
/// <param name="AuthorId">The author identifier.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="TagIds">The list of tag identifiers.</param>
public record BlogDetail(long Id, string Title, string Slug, string Content, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);

/// <summary>
/// Request to create a new blog post.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Content">The post content.</param>
/// <param name="Excerpt">The post excerpt.</param>
/// <param name="FeaturedImageUrl">The featured image URL.</param>
/// <param name="AuthorId">The author identifier.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="TagIds">The list of tag identifiers.</param>
public record CreateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long AuthorId, long CategoryId, IReadOnlyList<long> TagIds);

/// <summary>
/// Request to update an existing blog post.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Slug">The unique slug.</param>
/// <param name="Content">The post content.</param>
/// <param name="Excerpt">The post excerpt.</param>
/// <param name="FeaturedImageUrl">The featured image URL.</param>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="TagIds">The list of tag identifiers.</param>
public record UpdateBlogRequest(string Title, string Slug, string Content, string? Excerpt, string? FeaturedImageUrl, long CategoryId, IReadOnlyList<long> TagIds);
